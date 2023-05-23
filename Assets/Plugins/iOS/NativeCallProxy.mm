#import <Foundation/Foundation.h>
#import "NativeCallProxy.h"


@implementation FrameworkLibAPI

id<NativeCallsProtocol> api = NULL;
+(void) registerAPIforNativeCalls:(id<NativeCallsProtocol>) aApi
{
    api = aApi;
}

@end


extern "C" {
    void onAvatarLoadCompleted(const char* avatarName) { return [api onAvatarLoadCompleted:[NSString stringWithUTF8String:avatarName]]; }
    void onInitialized() { return [api onInitialized]; }
}

