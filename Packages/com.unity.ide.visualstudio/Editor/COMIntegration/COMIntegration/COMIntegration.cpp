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
static std::wstring_convert<std::codecvt_utf8<wchar_t>> converter;

#define RETRY_INTERVAL_MS 50
#define TIMEOUT_MS 120000

#define RETURN_ON_FAIL(expression) \
    result = ( expression );    \
    if ( FAILED( result ) )     \
    {                           \
        std::cout << #expression" result = 0x" << std::hex << result << std::endl;  \
        ClearProgressbar();     \
        return false;           \
    }                           \
    else // To prevent danging else condition

template<typename TSourceString, typename TDestString>
inline void ConvertUnityPathName(const TSourceString& utf8, TDestString& widePath)
{
	widePath = converter.from_bytes(utf8);
	std::replace(widePath.begin(), widePath.end(), L'/', L'\\'); // Convert separators to Windows
}

void UTF8ToWide(const char* utf8, wchar_t* outBuffer, int outBufferSize)
{
	int res = ::MultiByteToWideChar(CP_UTF8, 0, utf8, -1, outBuffer, outBufferSize);
	if (res == 0)
		outBuffer[0] = 0;
}

void ClearProgressbar() {
	std::cout << "clearprogressbar" << std::endl;
}

void ConvertSeparatorsToUnity(std::string& pathName)
{
	typename std::string::iterator it = pathName.begin(), itEnd = pathName.end();
	while (it != itEnd)
	{
		if (*it == '\\')
			*it = '/';
		++it;
	}
}

static void ConvertSeparatorsToUnity(char* pathName)
{
	while (*pathName != '\0')
	{
		if (*pathName == '\\')
			*pathName = '/';
		++pathName;
	}
}

void ConvertWindowsPathName(const wchar_t* widePath, char* outBuffer, int outBufferSize)
{
	::WideCharToMultiByte(CP_UTF8, 0, widePath, -1, outBuffer, outBufferSize, nullptr, nullptr);
	ConvertSeparatorsToUnity(outBuffer);
}

inline std::wstring QuoteString(const std::wstring& str)
{
	return L"\"" + str + L"\"";
}

void ConvertSeparatorsToWindows(wchar_t *pathName) {
    while (*pathName != L'\0') {
        if (*pathName == L'/')
            *pathName = L'\\';
        ++pathName;
    }
}

void ConvertUnityPathName(const char *utf8, wchar_t *outBuffer, int outBufferSize) {
    UTF8ToWide(utf8, outBuffer, outBufferSize);
    ConvertSeparatorsToWindows(outBuffer);
}

static bool BeginsWith(const std::string& str, const std::string& prefix)
{
	return str.size() >= prefix.size() && 0 == str.compare(0, prefix.size(), prefix);
}

std::string ErrorCodeToMsg(DWORD code)
{
	LPWSTR msgBuf = nullptr;
	if (!FormatMessageW(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS,
						nullptr, code, MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT), (LPWSTR)&msgBuf, 0, nullptr))
	{
		char buf[100];
		snprintf(buf, 100, "Unknown error [%i]", code);
		return buf;
	}
	else
	{
		std::string msg = converter.to_bytes(msgBuf);
		LocalFree(msgBuf);
		return msg;
	}
}


// Get an environment variable as a core::string:
static std::string GetEnvironmentVariableValue(const char *variableName) {
	DWORD currentBufferSize = MAX_PATH;
	std::wstring variableValue;
	variableValue.resize(currentBufferSize);

	DWORD requiredBufferSize = GetEnvironmentVariableW(converter.from_bytes(variableName).c_str(), &variableValue[0],
		currentBufferSize);
	if (requiredBufferSize == 0) {
		// Environment variable probably does not exist.
		return std::string();
	}
	else if (currentBufferSize < requiredBufferSize) {
		variableValue.resize(requiredBufferSize);
		if (GetEnvironmentVariableW(converter.from_bytes(variableName).c_str(), &variableValue[0], currentBufferSize) == 0)
			return std::string();
	}

	return converter.to_bytes(variableValue.c_str());
}

static bool StartVisualStudioProcess(const std::string &vsExe, const std::string &solutionFile, DWORD *dwProcessId) {
	STARTUPINFOW si;
	PROCESS_INFORMATION pi;
	BOOL result;

	ZeroMemory(&si, sizeof(si));
	si.cb = sizeof(si);
	ZeroMemory(&pi, sizeof(pi));

	// Get the normalized full path to VS
	WCHAR vsFullPath[kDefaultPathBufferSize];
	GetFullPathNameW(converter.from_bytes(vsExe).c_str(), kDefaultPathBufferSize, vsFullPath, nullptr);

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
	commandLineStream << QuoteString(vsFullPath).c_str() << L" ";

	std::wstring vsArgsWide = converter.from_bytes(GetEnvironmentVariableValue("UNITY_VS_ARGS"));
	if (vsArgsWide.length() > 0)
		commandLineStream << vsArgsWide << L" ";

	WCHAR solutionFileWide[kDefaultPathBufferSize];
	ConvertUnityPathName(solutionFile.c_str(), solutionFileWide, kDefaultPathBufferSize);

	commandLineStream << QuoteString(solutionFileWide).c_str();

	std::wstring commandLine = commandLineStream.str();

	// custom buffer, must be writable as CreateProcessW can alter it
	LPWSTR commandLineBuffer = new WCHAR[commandLine.size() + 1]();
	memcpy(commandLineBuffer, commandLine.c_str(), (commandLine.size() + 1) * sizeof(WCHAR));

	std::cout << "Starting Visual Studio process with: " << converter.to_bytes(commandLine) << std::endl;

	result = CreateProcessW(
		vsFullPath,     // Full path to VS, must not be quoted
		commandLineBuffer, // Command line, as passed as argv, separate arguments must be quoted if they contain spaces
		nullptr,        // Process handle not inheritable
		nullptr,        // Thread handle not inheritable
		FALSE,          // Set handle inheritance to FALSE
		0,              // No creation flags
		nullptr,        // Use parent's environment block
		startingDirectory.c_str(),     // starting directory set to the VS directory
		&si,
		&pi);

	if (!result) {
		DWORD error = GetLastError();
		std::cout << "Starting Visual Studio process failed: " << ErrorCodeToMsg(error) << std::endl;
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
	const std::string &visualStudioInstallationPathToUse,
	const std::string &solutionPathToFind)
{
	HRESULT result;
	win::ComPtr<IUnknown> punk = nullptr;
	win::ComPtr<EnvDTE::_DTE> dte = nullptr;

	std::wstring wideSolutionPath;
	ConvertUnityPathName(solutionPathToFind, wideSolutionPath);

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

		char charVisualStudioExecutablePath[kDefaultPathBufferSize];
		ConvertWindowsPathName(visualStudioExecutablePath, charVisualStudioExecutablePath, kDefaultPathBufferSize);

		std::string currentVisualStudioExecutablePath(charVisualStudioExecutablePath);
		ConvertSeparatorsToUnity(currentVisualStudioExecutablePath);

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
			std::cout << "We found for a running Visual Studio session with the solution open." << std::endl;
			if (!visualStudioInstallationPathToUse.empty()) {
				if (BeginsWith(currentVisualStudioExecutablePath, visualStudioInstallationPathToUse)) {
					return dte;
				}
				else {
					std::cout << "This running Visual Studio session does not seem to be the version requested in the user preferences. We will keep looking." << std::endl;
				}
			}
			else {
				std::cout << "We're not sure which version of Visual Studio was requested in the user preferences. We will use this running session." << std::endl;
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
	std::string monikerName = converter.to_bytes(oleMonikerName);

	// VisualStudio Moniker is "!VisualStudio.DTE.$Version:$PID"
	// Example "!VisualStudio.DTE.14.0:1234"

	if (monikerName.find("!VisualStudio.DTE") != 0)
		return false;

	std::ostringstream suffixStream;
	suffixStream << ":";
	suffixStream << dwProcessId;

	std::string suffix(suffixStream.str());

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

bool HaveRunningVSProOpenFile(const win::ComPtr<EnvDTE::_DTE> &dte, const std::string &_filename, int line) {
	HRESULT result;

	wchar_t filename[kDefaultPathBufferSize];
	ConvertUnityPathName(_filename.c_str(), filename, kDefaultPathBufferSize);

	BStrHolder bstrFileName(filename);
	BStrHolder bstrKind(converter.from_bytes(EnvDTE::vsViewKindPrimary).c_str());
	win::ComPtr<EnvDTE::Window> window = nullptr;

	if (!_filename.empty()) {
		std::cout << "Getting operations API from the Visual Studio session." << std::endl;

		win::ComPtr<EnvDTE::ItemOperations> item_ops;
		RETURN_ON_FAIL(dte->get_ItemOperations(&item_ops));

		std::cout << "Waiting for the Visual Studio session to open the file: " << converter.to_bytes(bstrFileName).c_str() << "." << std::endl;

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
	const std::string &userPreferenceVisualStudioInstallationPath,
	const std::string &filename,
	const std::string &solutionPath,
	int line)
{
	HRESULT result;
	win::ComPtr<EnvDTE::_DTE> dte = nullptr;

	std::cout << "Looking for a running Visual Studio session." << std::endl;
	std::string vsPath = userPreferenceVisualStudioInstallationPath;
	ConvertSeparatorsToUnity(vsPath);

	// TODO: If path does not exist pass empty, which will just try to match all windows with solution
	dte = FindRunningVSProWithOurSolution(vsPath, solutionPath);

	if (!dte) {
		std::cout << "No appropriate running Visual Studio session not found, creating a new one." << std::endl;

		//DisplayProgressbar("Opening Visual Studio", "Starting up Visual Studio, this might take some time.", .5f, true);
		std::cout << "displayProgressBar" << std::endl;

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

			Sleep(RETRY_INTERVAL_MS);
			timeWaited += RETRY_INTERVAL_MS;
		}

		ClearProgressbar();

		if (!dte)
			return false;

		std::cout << "Waiting for the newly launched Visual Studio session to be ready." << std::endl;

		EnvDTE::Window *window = nullptr;

		RETURN_ON_FAIL(dte->get_MainWindow(&window));
	}
	else {
		std::cout << "Using the existing Visual Studio session." << std::endl;
	}

	bool res = HaveRunningVSProOpenFile(dte, filename, line);

	ClearProgressbar();
	return res;
}

int main(int argc, char* argv[]) {
	if (argc != 5) {
		std::cerr << argc << ": wrong number of arguments\n" << "Usage: com.exe installationPath fileName solutionPath lineNumber" << std::endl;
		for (int i = 0; i < argc; i++) {
			std::cerr << argv[i] << std::endl;
		}
		return EXIT_FAILURE;
	}
	std::string installationPath(argv[1]);
	std::string fileName(argv[2]);
	std::string solutionPath(argv[3]);
	int lineNumber = atoi(argv[4]);

	CoInitialize(nullptr);
	VSPro_OpenFile_COM(installationPath, fileName, solutionPath, lineNumber);
	return EXIT_SUCCESS;
}
