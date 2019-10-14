#import  <Cocoa/Cocoa.h>
#import  <Foundation/Foundation.h>
#include <iostream>
#include <string>
#include <vector>

#define keyFileSender                                   'FSnd'

// 16 bit aligned legacy struct - this should total 20 bytes
struct TheSelectionRange
{
    int16_t unused1;    // 0 (not used)
    int16_t lineNum;    // line to select (<0 to specify range)
    int32_t startRange; // start of selection range (if line < 0)
    int32_t endRange;   // end of selection range (if line < 0)
    int32_t unused2;    // 0 (not used)
    int32_t theDate;    // modification date/time
} __attribute__((packed)); a

static NSString* MakeNSString(const std::string& string)
{
    NSString* ret = [NSString stringWithUTF8String: string.c_str()];
    return ret;
}

static UInt32 GetCreatorOfThisApp()
{
    static UInt32 creator = 0;
    if (creator == 0)
    {
        UInt32 type;
        CFBundleGetPackageInfo(CFBundleGetMainBundle(), &type, &creator);
    }
    return creator;
}

static BOOL OpenFileAtLineWithAppleEvent(NSRunningApplication *runningApp, NSString* path, int line, char* error, int errorLength)
{
    if (!runningApp)
        return NO;
    
    NSURL *pathUrl = [NSURL fileURLWithPath: path];
    
    if (line != -1)
        snprintf(error, errorLength, "%sAttempting to open file (using Apple events): %s line: %d.\n", error, [path UTF8String], line);
    else
        snprintf(error, errorLength, "%sAttempting to open file (using Apple events): %s.\n", error, [path UTF8String]);
    
    NSAppleEventDescriptor* targetDescriptor = [NSAppleEventDescriptor descriptorWithDescriptorType: typeApplicationBundleID data: [runningApp.bundleIdentifier dataUsingEncoding: NSUTF8StringEncoding]];
    NSAppleEventDescriptor* appleEvent = [NSAppleEventDescriptor appleEventWithEventClass: kCoreEventClass eventID: kAEOpenDocuments targetDescriptor: targetDescriptor returnID: kAutoGenerateReturnID transactionID: kAnyTransactionID];
    AEDesc reply = { typeNull, NULL };
    
    [appleEvent setParamDescriptor: [NSAppleEventDescriptor descriptorWithDescriptorType: typeFileURL data: [[pathUrl absoluteString] dataUsingEncoding: NSUTF8StringEncoding]] forKeyword: keyDirectObject];
    
#if 0
    // The last code bit that has anything to do with FSRef. By now, any application out there should easily support typeFileURL,
    // but maybe there is some legacy application where passing this (alongside with typeFileURL) is required. Until such reason exists, this
    // code remains disabled since it's using a deprecated API
    
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wdeprecated-declarations"
    FSRef fileFSRef;
    CFURLGetFSRef((CFURLRef)pathUrl, &fileFSRef);
#pragma clang diagnostic pop
    [appleEvent setParamDescriptor: [NSAppleEventDescriptor descriptorWithDescriptorType: typeFSRef bytes: &fileFSRef length: sizeof(fileFSRef)] forKeyword: keyDirectObject];
#endif
    
    UInt32 packageCreator = GetCreatorOfThisApp();
    if (packageCreator == kUnknownType)
    {
        [appleEvent setParamDescriptor: [NSAppleEventDescriptor descriptorWithDescriptorType: typeApplicationBundleID data: [[[NSBundle mainBundle] bundleIdentifier] dataUsingEncoding: NSUTF8StringEncoding]] forKeyword: keyFileSender];
    }
    else
    {
        [appleEvent setParamDescriptor: [NSAppleEventDescriptor descriptorWithTypeCode: packageCreator] forKeyword: keyFileSender];
    }
    
    if (line != -1)
    {
        // Add selection range to event
        TheSelectionRange range;
        range.unused1 = 0;
        range.lineNum = line - 1;
        range.startRange = -1;
        range.endRange = -1;
        range.unused2 = 0;
        range.theDate = -1;
        
        [appleEvent setParamDescriptor: [NSAppleEventDescriptor descriptorWithDescriptorType: typeChar bytes: &range length: sizeof(TheSelectionRange)] forKeyword: keyAEPosition];
    }
    
    OSErr err = AESendMessage([appleEvent aeDesc], &reply, kAENoReply + kAENeverInteract, kAEDefaultTimeout);
    if (err != noErr)
    {
        snprintf(error, errorLength, "%sFailed to open file: unable to send Apple event (error: %d).\n", error, err);
        return NO;
    }
    
    return YES;
}

static NSRunningApplication *LaunchServicesOpenApp(NSString* appPath)
{
    // Check is the desired app is already running
    NSRunningApplication *runningApp;
    NSURL *appUrl = [NSURL fileURLWithPath: appPath];
    NSWorkspace *workspace = [NSWorkspace sharedWorkspace];
    NSArray *runningApps = [workspace runningApplications];
    for (NSUInteger i = 0; i != runningApps.count; i++)
    {
        NSRunningApplication *app = [runningApps objectAtIndex: i];
        if ([app.bundleURL isEqual: appUrl] || [app.executableURL isEqual: appUrl])
        {
            runningApp = app;
            break;
        }
    }
    
    if (!runningApp || runningApp.isTerminated)
    {
        NSMutableDictionary* config = [[NSMutableDictionary alloc] init];
        runningApp = [[NSWorkspace sharedWorkspace] launchApplicationAtURL: appUrl options: NSWorkspaceLaunchDefault configuration: config error: nil];
    }
    
    if (runningApp)
        [runningApp activateWithOptions: 0];
    
    return runningApp;
}

bool LaunchOrReuseApp(const std::string& appPath, NSRunningApplication** outApp, char* error, int errorLength)
{
    std::vector<std::string> args;
    NSRunningApplication* app;
    
    app = LaunchServicesOpenApp(MakeNSString(appPath));
    
    if (outApp)
        *outApp = app;
    if (!app)
    {
        snprintf(error, errorLength, "%sFailed to open file: unable to launch the application at %s.\n", error, appPath.c_str());
        return false;
    }
    return true;
}

bool MonoDevelopOpenFile(const std::string& appPath, const std::string& solutionPath, const std::string& filePath, int line, char* error, int errorLength)
{
    NSRunningApplication* runningApp;
    if (!LaunchOrReuseApp(appPath, &runningApp, error, errorLength))
        return false;
    
    OpenFileAtLineWithAppleEvent(runningApp, MakeNSString(solutionPath), -1, error, errorLength);
    
    // Do not try to open an empty filepath
    return filePath.empty() ? true : OpenFileAtLineWithAppleEvent(runningApp, MakeNSString(filePath), line, error, errorLength);
}

#if BUILD_APP
int main(int argc, const char * argv[]) {
    
    if (argc != 5) {
        std::cerr << argc << ": wrong number of arguments\n" << "Usage: AppleEventIntegration.exe installationPath fileName solutionPath lineNumber" << std::endl;
        for (int i = 0; i < argc; i++) {
            std::cerr << argv[i] << std::endl;
        }
        return 1;
    }
    const std::string installationPath(argv[1]);
    const std::string fileName(argv[2]);
    const std::string solutionPath(argv[3]);
    const int lineNumber = atoi(argv[4]);
    
    @autoreleasepool
    {
        char errorBuffer[4096];
        errorBuffer[0] = '\0';
        
        MonoDevelopOpenFile(installationPath, solutionPath, fileName, lineNumber, errorBuffer, 4096);
        
        if(strlen(errorBuffer) > 0)
        {
            puts(errorBuffer);
        }        
    }
    
    printf("AppleEventIntegration.exe Exit");
    
    return 0;
}
#else

extern "C"
{
    
void OpenVisualStudio(const char* appPath, const char* solutionPath, const char* filePath, int line, char* error, int errorLength)
{
    MonoDevelopOpenFile(appPath, solutionPath, filePath, line, error, errorLength);
}
    
}

#endif
