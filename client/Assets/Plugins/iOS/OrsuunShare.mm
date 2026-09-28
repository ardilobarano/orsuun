// The share sheet on iOS (INVITE A FRIEND's SHARE): Apple's UIActivityViewController with one line of text.
#import <UIKit/UIKit.h>

extern UIViewController* UnityGetGLViewController(void);

extern "C" void OrsuunShare_Text(const char* text)
{
    NSString* line = [NSString stringWithUTF8String:text];
    UIActivityViewController* sheet = [[UIActivityViewController alloc] initWithActivityItems:@[line] applicationActivities:nil];
    UIViewController* root = UnityGetGLViewController();
    // iPads show the sheet as a popover, which needs an anchor.
    sheet.popoverPresentationController.sourceView = root.view;
    sheet.popoverPresentationController.sourceRect = CGRectMake(root.view.bounds.size.width / 2, root.view.bounds.size.height / 2, 1, 1);
    [root presentViewController:sheet animated:YES completion:nil];
}
