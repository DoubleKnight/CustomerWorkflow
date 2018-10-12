using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Activities;

namespace CustomerWorkflow
{
    public sealed class CompleteStep : NativeActivity
    {
        [RequiredArgument]
        public InArgument<string> BookmarkName { get; set; }

        protected override void Execute(NativeActivityContext context)
        {
            string name = BookmarkName.Get(context);

            if (name == string.Empty)
            {
                throw new ArgumentException("BookmarkName cannot be an Empty string.",
                    "BookmarkName");
            }

            context.CreateBookmark(name, OnComplete);
        }

        protected override bool CanInduceIdle
        {
            get { return true; }
        }

        void OnComplete(NativeActivityContext context, Bookmark bookmark, object state)
        {
            Console.WriteLine("CompleteStep.OnComplete");
        }
    }
}
