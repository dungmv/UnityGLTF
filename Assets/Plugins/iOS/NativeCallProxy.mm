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
    void onAvatarLoadCompleted(const char* avatarId) { [api onAvatarLoadCompleted:[NSString stringWithUTF8String:avatarId]]; }
    void onInitialized() { [api onInitialized]; }
    
    void registerLoadAvatarDelegate(LoadAvatarDelegate func) { [api registerLoadAvatarDelegate: func]; }
    void registerSetBackgroundColorDelegate(SetBackgroundColorCallback func) { [api registerSetBackgroundColorDelegate: func]; }
    void registerSetFoVDelegate(SetFoVDelegate func) { [api registerSetFoVDelegate: func]; }
    void registerRunAnimationDelegate(RunAnimationDelegate func) { [api registerRunAnimationDelegate: func]; }
    void registerSetPositionAvatarDelegate(SetPositionAvatarDelegate func) { [api registerSetPositionAvatarDelegate: func]; }
    void registerSetPositionCameraDelegate(SetPositionCameraDelegate func) { [api registerSetPositionCameraDelegate: func]; }
    
}
