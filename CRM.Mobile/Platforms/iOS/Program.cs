#if IOS
using ObjCRuntime;
using UIKit;

namespace CRM.Mobile;

public class Program
{
    private static void Main(string[] args)
    {
        UIApplication.Main(args, null, typeof(AppDelegate));
    }
}
#endif
