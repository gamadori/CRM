using BlazoringComponents;
using Microsoft.AspNetCore.Components;
using Radzen;
using System;

namespace CRM.Client.Pages.ProductParameters
{
    

    public partial class UploadData: ComponentBase
    {
        private EventConsole console;
        void OnComplete(UploadCompleteEventArgs args)
        {
            console.Log($"Upload complete with server response: {args.RawResponse}");
        }

        void OnProgress(UploadProgressArgs args)
        {
            console.Log($"Upload progress: {args.Progress}% / {args.Loaded} of {args.Total} bytes.");

            if (args.Progress == 100)
            {
                foreach (var file in args.Files)
                {
                    console.Log($"Uploaded: {file.Name} / {file.Size} bytes");
                }
            }
        }
    }
}
