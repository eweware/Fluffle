// WARNING
//
// This file has been generated automatically by Xamarin Studio from the outlets and
// actions declared in your storyboard file.
// Manual changes to this file will not be maintained.
//
using Foundation;
using System;
using System.CodeDom.Compiler;
using UIKit;

namespace Fluffimax.iOS
{
	[Register ("LBPlayerShareVC")]
	partial class LBPlayerShareVC
	{
		[Outlet]
		[GeneratedCode ("iOS Designer", "1.0")]
		UITableView DataTable { get; set; }

		[Outlet]
		[GeneratedCode ("iOS Designer", "1.0")]
		NSLayoutConstraint TopConstraint { get; set; }

		void ReleaseDesignerOutlets ()
		{
			if (DataTable != null) {
				DataTable.Dispose ();
				DataTable = null;
			}
			if (TopConstraint != null) {
				TopConstraint.Dispose ();
				TopConstraint = null;
			}
		}
	}
}
