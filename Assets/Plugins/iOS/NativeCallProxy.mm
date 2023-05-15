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
    void onAvatarLoadCompleted(const char* url) { return [api onAvatarLoadCompleted:[NSString stringWithUTF8String:url]]; }
    void onInitialized() { return [api onInitialized]; }
}

