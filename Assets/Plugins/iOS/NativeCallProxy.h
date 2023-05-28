// [!] important set UnityFramework in Target Membership for this file
// [!]           and set Public header visibility

#import <Foundation/Foundation.h>

typedef void (*LoadAvatarDelegate)(const char* id, const char* url, const char* name, const char* status, float x, float y, float z);
typedef void (*SetBackgroundColorCallback)(const char* color);
typedef void (*SetFoVDelegate)(float fov);
typedef void (*RunAnimationDelegate)(const char* id, int animid);
typedef void (*SetPositionAvatarDelegate)(const char* id, float x, float y, float z);
typedef void (*SetPositionCameraDelegate)(float x, float y, float z);

// NativeCallsProtocol defines protocol with methods you want to be called from managed
@protocol NativeCallsProtocol
@required
- (void) onAvatarLoadCompleted:(NSString*)avatarId;
- (void) onInitialized;
// setup delegate
- (void) registerLoadAvatarDelegate:(LoadAvatarDelegate) cb;
- (void) registerSetBackgroundColorDelegate:(SetBackgroundColorCallback) cb;
- (void) registerSetFoVDelegate:(SetFoVDelegate) cb;
- (void) registerRunAnimationDelegate:(RunAnimationDelegate) cb;
- (void) registerSetPositionAvatarDelegate:(SetPositionAvatarDelegate) cb;
- (void) registerSetPositionCameraDelegate:(SetPositionCameraDelegate) cb;
@end

__attribute__ ((visibility("default")))
@interface FrameworkLibAPI : NSObject
// call it any time after UnityFrameworkLoad to set object implementing NativeCallsProtocol methods
+(void) registerAPIforNativeCalls:(id<NativeCallsProtocol>) aApi;

@end


