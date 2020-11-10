/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Unity Technologies.
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
// COMIntegration.cpp : This file contains the 'main' function. Program execution begins and ends there.
//

#include <iostream>
#include <sstream>
#include <codecvt>
#include <string>
#include <algorithm>
#include <windows.h>
#include <shlwapi.h>

#include "BStrHolder.h"
#include "ComPtr.h"
#include "dte80a.tlh"

const int kDefaultPathBufferSize = MAX_PATH * 4;

#define RETRY_INTERVAL_MS 101
#define TIMEOUT_MS 10000

#define RETURN_ON_FAIL(expression) \
	result = ( expression );	\
	if ( FAILED( result ) )		\
	{							\
		std::wcout << #expression" result = 0x" << std::hex << result << std::endl;  \
		ClearProgressbar();	 	\
		return false;			\
	}							\
	else // To prevent danging else condition

// Often a DTE call made to Visual Studio can fail after Visual Studio has just started. Usually the
// return value will be RPC_E_CALL_REJECTED, meaning that Visual Studio is probably busy on another
// thread. This types filter the RPC messages and retries to send the message until VS accepts it.
class CRetryMessageFilterBase : public IMessageFilter
{
private:
	static bool ShouldRetryCall(DWORD dwTickCount, DWORD dwRejectType)
	{
		if (dwRejectType == SERVERCALL_RETRYLATER || dwRejectType == SERVERCALL_REJECTED)
			return dwTickCount < TIMEOUT_MS;

		return false;
	}

protected:
	win::ComPtr<IMessageFilter> currentFilter;

public:
	// IUnknown methods
	IFACEMETHODIMP QueryInterface(REFIID riid, void** ppv)
	{
		static const QITAB qit[] =
		{
			QITABENT(CRetryMessageFilterBase, IMessageFilter),
			{ 0 },
		};
		return QISearch(this, qit, riid, ppv);
	}

	IFACEMETHODIMP_(ULONG) AddRef()
	{
		return 0;
	}

	IFACEMETHODIMP_(ULONG) Release()
	{
		return 0;
	}

	DWORD STDMETHODCALLTYPE HandleInComingCall(DWORD dwCallType, HTASK htaskCaller, DWORD dwTickCount, LPINTERFACEINFO lpInterfaceInfo)
	{
		if (currentFilter)
			return currentFilter->HandleInComingCall(dwCallType, htaskCaller, dwTickCount, lpInterfaceInfo);

		return SERVERCALL_ISHANDLED;
	}

	DWORD STDMETHODCALLTYPE RetryRejectedCall(HTASK htaskCallee, DWORD dwTickCount, DWORD dwRejectType)
	{
		if (ShouldRetryCall(dwTickCount, dwRejectType))
			return RETRY_INTERVAL_MS;

		if (currentFilter)
			return currentFilter->RetryRejectedCall(htaskCallee, dwTickCount, dwRejectType);

		return (DWORD)-1;
	}

	DWORD STDMETHODCALLTYPE MessagePending(HTASK htaskCallee, DWORD dwTickCount, DWORD dwPendingType)
	{
		if (currentFilter)
			return currentFilter->MessagePending(htaskCallee, dwTickCount, dwPendingType);

		return PENDINGMSG_WAITDEFPROCESS;
	}
};

class CRetryMessageFilter :
	public CRetryMessageFilterBase
{
public:
	CRetryMessageFilter()
	{
		HRESULT hr = CoRegisterMessageFilter(this, &currentFilter);
		_ASSERT(SUCCEEDED(hr));
	}

	~CRetryMessageFilter()
	{
		win::ComPtr<IMessageFilter> messageFilter;
		HRESULT hr = CoRegisterMessageFilter(currentFilter, &messageFilter);
		_ASSERT(SUCCEEDED(hr));
	}
};

void ClearProgressbar() {
	std::wcout << "clearprogressbar" << std::endl;
}

static std::wstring ReplaceAll(std::wstring str, const std::wstring& from, const std::wstring& to) {
    size_t start_pos = 0;
    while((start_pos = str.find(from, start_pos)) != std::string::npos) {
        str.replace(start_pos, from.length(), to);
        start_pos += to.length(); // Handles case where 'to' is a substring of 'from'
    }
    return str;
}

static void ConvertSeparatorsToUnix(std::wstring& pathName)
{
    pathName = ReplaceAll(pathName, L"\\", L"/");
}

inline std::wstring QuoteString(const std::wstring& str)
{
	return L"\"" + str + L"\"";
}

void ConvertSeparatorsToWindows(std::wstring& pathName) {
    pathName = ReplaceAll(pathName, L"/", L"\\");
}

static bool BeginsWith(const std::wstring& str, const std::wstring& prefix)
{
	return str.size() >= prefix.size() && 0 == str.compare(0, prefix.size(), prefix);
}

std::wstring ErrorCodeToMsg(DWORD code)
{
	LPWSTR msgBuf = nullptr;
	if (!FormatMessageW(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS,
						nullptr, code, MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT), (LPWSTR)&msgBuf, 0, nullptr))
	{
        return L"Unknown error";
	}
	else
	{
		return msgBuf;
	}
}


// Get an environment variable as a core::string:
static std::wstring GetEnvironmentVariableValue(std::wstring variableName) {
	DWORD currentBufferSize = MAX_PATH;
	std::wstring variableValue;
	variableValue.resize(currentBufferSize);

	DWORD requiredBufferSize = GetEnvironmentVariableW(variableName.c_str(), &variableValue[0], currentBufferSize);
	if (requiredBufferSize == 0) {
		// Environment variable probably does not exist.
		return std::wstring();
	}
	else if (currentBufferSize < requiredBufferSize) {
		variableValue.resize(requiredBufferSize);
		if (GetEnvironmentVariableW(variableName.c_str(), &variableValue[0], currentBufferSize) == 0)
			return std::wstring();
	}

	return variableValue;
}

static bool StartVisualStudioProcess(const std::wstring &vsExe, const std::wstring &solutionFile, DWORD *dwProcessId) {
	STARTUPINFOW si;
	PROCESS_INFORMATION pi;
	BOOL result;

	ZeroMemory(&si, sizeof(si));
	si.cb = sizeof(si);
	ZeroMemory(&pi, sizeof(pi));

	// Get the normalized full path to VS
	WCHAR vsFullPath[kDefaultPathBufferSize];
	GetFullPathNameW(vsExe.c_str(), kDefaultPathBufferSize, vsFullPath, nullptr);

	// Extra the VS directory to use it as the
	// starting directory for the VS process.
	WCHAR vsStartingDirectory[kDefaultPathBufferSize];
	WCHAR vsDrive[10];
	_wsplitpath(vsFullPath, vsDrive, vsStartingDirectory, nullptr, nullptr);

	std::wstringstream startingDirectoryStream;
	startingDirectoryStream << vsDrive << vsStartingDirectory;
	std::wstring startingDirectory = startingDirectoryStream.str();

	// Build the command line that is passed as the argv of the VS process
	// argv[0] must be the quoted full path to the VS exe
	std::wstringstream commandLineStream;
	commandLineStream << QuoteString(vsFullPath) << L" ";

	std::wstring vsArgsWide = GetEnvironmentVariableValue(L"UNITY_VS_ARGS");
	if (vsArgsWide.length() > 0)
		commandLineStream << vsArgsWide << L" ";

	std::wstring solutionFileWide(solutionFile);
	ConvertSeparatorsToWindows(solutionFileWide);

	commandLineStream << QuoteString(solutionFileWide);

	std::wstring commandLine = commandLineStream.str();

	// custom buffer, must be writable as CreateProcessW can alter it
	LPWSTR commandLineBuffer = new WCHAR[commandLine.size() + 1]();
	memcpy(commandLineBuffer, commandLine.c_str(), (commandLine.size() + 1) * sizeof(WCHAR));

	std::wcout << "Starting Visual Studio process with: " << commandLine << std::endl;

	result = CreateProcessW(
		vsFullPath,					// Full path to VS, must not be quoted
		commandLineBuffer,			// Command line, as passed as argv, separate arguments must be quoted if they contain spaces
		nullptr,					// Process handle not inheritable
		nullptr,					// Thread handle not inheritable
		FALSE,						// Set handle inheritance to FALSE
		0,							// No creation flags
		nullptr,					// Use parent's environment block
		startingDirectory.c_str(),	// starting directory set to the VS directory
		&si,
		&pi);

	if (!result) {
		DWORD error = GetLastError();
		std::wcout << "Starting Visual Studio process failed: " << ErrorCodeToMsg(error) << std::endl;
	}

	delete[] commandLineBuffer;

	if (!result)
		return false;

	*dwProcessId = pi.dwProcessId;
	CloseHandle(pi.hProcess);
	CloseHandle(pi.hThread);

	return true;
}

win::ComPtr<EnvDTE::_DTE> FindRunningVSProWithOurSolution(
	const std::wstring &visualStudioInstallationPathToUse,
	const std::wstring &solutionPathToFind)
{
	HRESULT result;
	win::ComPtr<IUnknown> punk = nullptr;
	win::ComPtr<EnvDTE::_DTE> dte = nullptr;

	std::wstring wideSolutionPath(solutionPathToFind);
	ConvertSeparatorsToWindows(wideSolutionPath);

	// Search through the Running Object Table for an instance of Visual Studio
	// to use that either has the correct solution already open or does not have
	// any solution open.
	win::ComPtr<IRunningObjectTable> ROT;
	RETURN_ON_FAIL(GetRunningObjectTable(0, &ROT));

	win::ComPtr<IBindCtx> bindCtx;
	RETURN_ON_FAIL(CreateBindCtx(0, &bindCtx));

	win::ComPtr<IEnumMoniker> enumMoniker;
	RETURN_ON_FAIL(ROT->EnumRunning(&enumMoniker));

	win::ComPtr<IMoniker> moniker;
	ULONG monikersFetched = 0;
	while (enumMoniker->Next(1, &moniker, &monikersFetched) == S_OK) {
		punk = nullptr;
		result = ROT->GetObject(moniker, &punk);
		moniker = nullptr;
		if (result != S_OK)
			continue;

		punk.As(&dte);

		if (!dte)
			continue;
		// Okay, so we found an actual running instance of Visual Studio.

		// Get the executable path of this running instance.
		BStrHolder visualStudioExecutablePath;
		result = dte->get_FullName(&visualStudioExecutablePath);
		if (FAILED(result))
			continue;

		std::wstring currentVisualStudioExecutablePath(visualStudioExecutablePath);
		ConvertSeparatorsToUnix(currentVisualStudioExecutablePath);

		// Ask for its current solution.
		win::ComPtr<EnvDTE::_Solution> solution;
		result = dte->get_Solution(&solution);
		if (FAILED(result))
			continue;

		// Get the name of that solution.
		BStrHolder currentVisualStudioSolutionPath;
		result = solution->get_FullName(&currentVisualStudioSolutionPath);
		if (FAILED(result))
			continue;

		// If the name matches the solution we want to open and we have a Visual Studio installation path to use and this one matches that path, then use it.
		// If we don't have a Visual Studio installation path to use, just use this solution.
		if (std::wstring(currentVisualStudioSolutionPath) == wideSolutionPath) {
			std::wcout << "We found for a running Visual Studio session with the solution open." << std::endl;
			if (!visualStudioInstallationPathToUse.empty()) {
				if (BeginsWith(currentVisualStudioExecutablePath, visualStudioInstallationPathToUse)) {
					return dte;
				}
				else {
					std::wcout << "This running Visual Studio session does not seem to be the version requested in the user preferences. We will keep looking." << std::endl;
				}
			}
			else {
				std::wcout << "We're not sure which version of Visual Studio was requested in the user preferences. We will use this running session." << std::endl;
				return dte;
			}
		}
	}
	return nullptr;
}

static bool
MonikerIsVisualStudioProcess(const win::ComPtr<IMoniker> &moniker, const win::ComPtr<IBindCtx> &bindCtx, const DWORD dwProcessId) {
	LPOLESTR oleMonikerName;
	moniker->GetDisplayName(bindCtx, nullptr, &oleMonikerName);
	std::wstring monikerName(oleMonikerName);

	// VisualStudio Moniker is "!VisualStudio.DTE.$Version:$PID"
	// Example "!VisualStudio.DTE.14.0:1234"

	if (monikerName.find(L"!VisualStudio.DTE") != 0)
		return false;

	std::wstringstream suffixStream;
	suffixStream << ":";
	suffixStream << dwProcessId;

	std::wstring suffix(suffixStream.str());

	return monikerName.length() - suffix.length() == monikerName.find(suffix);
}

win::ComPtr<EnvDTE::_DTE> FindRunningVSProWithPID(const DWORD dwProcessId) {
	HRESULT result;
	win::ComPtr<IUnknown> punk = nullptr;
	win::ComPtr<EnvDTE::_DTE> dte = nullptr;

	// Search through the Running Object Table for a Visual Studio
	// process with the process ID specified
	win::ComPtr<IRunningObjectTable> ROT;
	RETURN_ON_FAIL(GetRunningObjectTable(0, &ROT));

	win::ComPtr<IBindCtx> bindCtx;
	RETURN_ON_FAIL(CreateBindCtx(0, &bindCtx));

	win::ComPtr<IEnumMoniker> enumMoniker;
	RETURN_ON_FAIL(ROT->EnumRunning(&enumMoniker));

	win::ComPtr<IMoniker> moniker;
	ULONG monikersFetched = 0;
	while (enumMoniker->Next(1, &moniker, &monikersFetched) == S_OK) {
		punk = nullptr;
		result = ROT->GetObject(moniker, &punk);
		if (result != S_OK)
			continue;

		bool isVs = MonikerIsVisualStudioProcess(moniker, bindCtx, dwProcessId);
		moniker = nullptr;

		if (!isVs)
			continue;

		punk.As(&dte);

		if (dte)
			return dte;
	}

	return nullptr;
}

bool HaveRunningVSProOpenFile(const win::ComPtr<EnvDTE::_DTE> &dte, const std::wstring &_filename, int line) {
	HRESULT result;

	std::wstring filename(_filename);
	ConvertSeparatorsToWindows(filename);

	BStrHolder bstrFileName(filename.c_str());
	BStrHolder bstrKind(L"{00000000-0000-0000-0000-000000000000}"); // EnvDTE::vsViewKindPrimary
	win::ComPtr<EnvDTE::Window> window = nullptr;

	CRetryMessageFilter retryMessageFilter;

	if (!_filename.empty()) {
		std::wcout << "Getting operations API from the Visual Studio session." << std::endl;

		win::ComPtr<EnvDTE::ItemOperations> item_ops;
		RETURN_ON_FAIL(dte->get_ItemOperations(&item_ops));

		std::wcout << "Waiting for the Visual Studio session to open the file: " << bstrFileName << "." << std::endl;

		RETURN_ON_FAIL(item_ops->OpenFile(bstrFileName, bstrKind, &window));

		if (line > 0) {
			win::ComPtr<IDispatch> selection_dispatch;
			if (window && SUCCEEDED(window->get_Selection(&selection_dispatch))) {
				win::ComPtr<EnvDTE::TextSelection> selection;
				if (selection_dispatch &&
					SUCCEEDED(selection_dispatch->QueryInterface(__uuidof(EnvDTE::TextSelection), &selection)) &&
					selection) {
					selection->GotoLine(line, TRUE);
				}
			}
		}
	}

	window = nullptr;
	if (SUCCEEDED(dte->get_MainWindow(&window))) {
		// Allow the DTE to make its main window the foreground
		HWND hWnd;
		window->get_HWnd((LONG *)&hWnd);

		DWORD processID;
		if (SUCCEEDED(GetWindowThreadProcessId(hWnd, &processID)))
			AllowSetForegroundWindow(processID);

		// Activate() set the window to visible and active (blinks in taskbar)
		window->Activate();
	}

	ClearProgressbar();

	return true;
}

bool VSPro_OpenFile_COM(
	const std::wstring &userPreferenceVisualStudioInstallationPath,
	const std::wstring &filename,
	const std::wstring &solutionPath,
	int line)
{
	HRESULT result;
	win::ComPtr<EnvDTE::_DTE> dte = nullptr;

	std::wcout << "Looking for a running Visual Studio session." << std::endl;
	std::wstring vsPath = userPreferenceVisualStudioInstallationPath;
	ConvertSeparatorsToUnix(vsPath);

	// TODO: If path does not exist pass empty, which will just try to match all windows with solution
	dte = FindRunningVSProWithOurSolution(vsPath, solutionPath);

	if (!dte) {
		std::wcout << "No appropriate running Visual Studio session not found, creating a new one." << std::endl;

		CRetryMessageFilter retryMessageFilter;

		//DisplayProgressbar("Opening Visual Studio", "Starting up Visual Studio, this might take some time.", .5f, true);
		std::wcout << "displayProgressBar" << std::endl;

		DWORD dwProcessId = 0;

		if (!StartVisualStudioProcess(userPreferenceVisualStudioInstallationPath, solutionPath, &dwProcessId)) {
			ClearProgressbar();
			return false;
		}

		int timeWaited = 0;

		while (timeWaited < TIMEOUT_MS) {
			const float progress = 0.5f + (((float)timeWaited) / ((float)TIMEOUT_MS)) * 0.5f;

			/*if (DisplayProgressbar("Opening Visual Studio", "Starting up Visual Studio, this might take some time.",
				progress, true) == kPBSWantsToCancel)
				break;*/

			dte = FindRunningVSProWithPID(dwProcessId);

			if (dte)
				break;

			std::wcout << "Retrying to acquire DTE" << std::endl;

			Sleep(RETRY_INTERVAL_MS);
			timeWaited += RETRY_INTERVAL_MS;
		}

		ClearProgressbar();

		if (!dte)
			return false;

		std::wcout << "Waiting for the newly launched Visual Studio session to be ready." << std::endl;

		//EnvDTE::Window *window = nullptr;

		//RETURN_ON_FAIL(dte->get_MainWindow(&window));
	}
	else {
		std::wcout << "Using the existing Visual Studio session." << std::endl;
	}

	bool res = HaveRunningVSProOpenFile(dte, filename, line);

	ClearProgressbar();
	return res;
}

int wmain(int argc, wchar_t* argv[]) {
	if (argc != 5) {
		std::wcerr << argc << ": wrong number of arguments\n" << "Usage: com.exe installationPath fileName solutionPath lineNumber" << std::endl;
		for (int i = 0; i < argc; i++) {
			std::wcerr << argv[i] << std::endl;
		}
		return EXIT_FAILURE;
	}
	std::wstring installationPath(argv[1]);
	std::wstring fileName(argv[2]);
	std::wstring solutionPath(argv[3]);
	int lineNumber = std::stoi(argv[4]);

	if (FAILED(CoInitialize(nullptr))) {
		std::wcout << "Using the existing Visual Studio session." << std::endl;
		return EXIT_FAILURE;
    }

	VSPro_OpenFile_COM(installationPath, fileName, solutionPath, lineNumber);
	return EXIT_SUCCESS;
}
