// Sign in with Google / Apple on iOS: the provider's page opens in Apple's in-app sign-in sheet
// (ASWebAuthenticationSession) and the orsuun:// link it ends on comes straight back to the game
// (ServerLink.OnAuthCallback). Works without any entitlement, so free signing still builds.
#import <AuthenticationServices/AuthenticationServices.h>
#import <UIKit/UIKit.h>

extern void UnitySendMessage(const char* obj, const char* method, const char* msg);
extern UIViewController* UnityGetGLViewController(void);

@interface OrsuunAuthAnchor : NSObject <ASWebAuthenticationPresentationContextProviding>
@end

@implementation OrsuunAuthAnchor
- (ASPresentationAnchor)presentationAnchorForWebAuthenticationSession:(ASWebAuthenticationSession*)session
{
    return UnityGetGLViewController().view.window;
}
@end

static ASWebAuthenticationSession* orsuunSession;
static OrsuunAuthAnchor* orsuunAnchor;

extern "C" void OrsuunAuth_Start(const char* url, const char* scheme)
{
    NSURL* start = [NSURL URLWithString:[NSString stringWithUTF8String:url]];
    NSString* callbackScheme = [NSString stringWithUTF8String:scheme];
    orsuunSession = [[ASWebAuthenticationSession alloc] initWithURL:start
                                                  callbackURLScheme:callbackScheme
                                                  completionHandler:^(NSURL* callback, NSError* error) {
        NSString* result = callback != nil ? callback.absoluteString : @"orsuun://auth?error=cancelled";
        UnitySendMessage("ServerLink", "OnAuthCallback", result.UTF8String);
        orsuunSession = nil;
    }];
    orsuunAnchor = [OrsuunAuthAnchor new];
    orsuunSession.presentationContextProvider = orsuunAnchor;
    orsuunSession.prefersEphemeralWebBrowserSession = NO;
    [orsuunSession start];
}
