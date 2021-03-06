﻿using System;

using UIKit;
using Fluffimax.Core;
using Foundation;
using System.Collections.Generic;
using StoreKit;

namespace Fluffimax.iOS
{
	public partial class CarrotShopViewController : UIViewController
	{
		public static string BuyCarrot01 = "com.eweware.fluffle.carrot01";
		public static string BuyCarrot02 = "com.eweware.fluffle.carrot02";
		public static string BuyCarrot03 = "com.eweware.fluffle.carrot03";
		public static string BuyCarrot04 = "com.eweware.fluffle.carrot04";
		public static string BuyCarrot05 = "com.eweware.fluffle.carrot05";

		NSObject priceObserver, succeededObserver, failedObserver, requestObserver;

		InAppPurchaseManager iap;
		List<string> products;
		bool pricesLoaded = false;

		public CarrotShopViewController () : base ("CarrotShopViewController", null)
		{
			products = new List<string>() { BuyCarrot01, BuyCarrot02, BuyCarrot03, BuyCarrot04, BuyCarrot05 };
			iap = new InAppPurchaseManager();
		}

		public override void ViewDidLoad ()
		{
			base.ViewDidLoad ();
			// Perform any additional setup after loading the view, typically from a nib.

			BuyItem1Btn.TouchUpInside += (object sender, EventArgs e) => {
				iap.PurchaseProduct(BuyCarrot01);
			};

			BuyItem2Btn.TouchUpInside += (object sender, EventArgs e) => {
				iap.PurchaseProduct(BuyCarrot02);
			};

			BuyItem3Btn.TouchUpInside += (object sender, EventArgs e) => {
				iap.PurchaseProduct(BuyCarrot03);
			};

			BuyItem4Btn.TouchUpInside += (object sender, EventArgs e) => {
				iap.PurchaseProduct(BuyCarrot04);
			};

			BuyItem5Btn.TouchUpInside += (object sender, EventArgs e) => {
				iap.PurchaseProduct(BuyCarrot05);
			};

			WatchAdBtn.TouchUpInside += (object sender, EventArgs e) => {
				HandleAdView();
			};

			this.Title = "Carrot_Shop".Localize();
			WatchAdBtn.Hidden = true;

			if (AppDelegate.IsMini)
			{
				// make things smaller
				UIFont smallFont = UIFont.FromName(Item1Title.Font.Name, 13);
				Item1Title.Font = smallFont;
				Item2Title.Font = smallFont;
				Item3Title.Font = smallFont;
				Item4Title.Font = smallFont;
				Item5Title.Font = smallFont;
				WatchAdBtn.Font = smallFont;
				View.LayoutIfNeeded();
			}



			var menuBtn = new UIBarButtonItem("back_btn".Localize(), UIBarButtonItemStyle.Bordered, null);
			this.NavigationItem.BackBarButtonItem = menuBtn;


		}
			
		public override void ViewDidAppear(bool animated)
		{
			HomeViewController.ShowTutorialStep("carrot_shop_tutorial", "carrot_shop_tutorial".Localize());

		}

		private void HandleAdView() {
			int carrotCount = 10;

			if (carrotCount > 0) {
				Game.CurrentPlayer.GiveCarrots (carrotCount);
				UpdateTextLabels ();
			}
		}

		private void UpdateTextLabels() {
			InvokeOnMainThread (() => {
				CurrentCarrotLabel.Text = String.Format("carrot_count".Localize(), Game.CurrentPlayer.carrotCount);
			});
		}

		public override void DidReceiveMemoryWarning ()
		{
			base.DidReceiveMemoryWarning ();
			// Release any cached data, images, etc that aren't in use.
		}

		public override void ViewWillAppear (bool animated)
		{
			base.ViewWillAppear (animated);
			NavController.NavigationBarHidden = false;
			UpdateTextLabels ();

			priceObserver = NSNotificationCenter.DefaultCenter.AddObserver (InAppPurchaseManager.InAppPurchaseManagerProductsFetchedNotification, 
				(notification) => {
					var info = notification.UserInfo;
					if (info == null) return;

					var NSBuyProduct01Id = new NSString(BuyCarrot01);
					var NSBuyProduct02Id = new NSString(BuyCarrot02);
					var NSBuyProduct03Id = new NSString(BuyCarrot03);
					var NSBuyProduct04Id = new NSString(BuyCarrot04);
					var NSBuyProduct05Id = new NSString(BuyCarrot05);

					if (info.ContainsKey(NSBuyProduct01Id)) {
						pricesLoaded = true;

						var product = (SKProduct) info.ObjectForKey(NSBuyProduct01Id);

						BuyItem1Btn.Enabled = true;
						Item1Title.Text = product.LocalizedTitle + " - " + product.LocalizedPrice();
						Item1Detail.Text = product.LocalizedDescription;

					}
					if (info.ContainsKey(NSBuyProduct02Id)) {
						pricesLoaded = true;

						var product = (SKProduct) info.ObjectForKey(NSBuyProduct02Id);

						BuyItem2Btn.Enabled = true;
						Item2Title.Text = product.LocalizedTitle + " - " + product.LocalizedPrice();
						Item2Detail.Text = product.LocalizedDescription;
					}

					if (info.ContainsKey(NSBuyProduct03Id)) {
						pricesLoaded = true;

						var product = (SKProduct) info.ObjectForKey(NSBuyProduct03Id);

						BuyItem3Btn.Enabled = true;
						Item3Title.Text = product.LocalizedTitle + " - " + product.LocalizedPrice();
						Item3Detail.Text = product.LocalizedDescription;
					}

					if (info.ContainsKey(NSBuyProduct04Id)) {
						pricesLoaded = true;

						var product = (SKProduct) info.ObjectForKey(NSBuyProduct04Id);

						BuyItem4Btn.Enabled = true;
						Item4Title.Text = product.LocalizedTitle + " - " + product.LocalizedPrice();
						Item4Detail.Text = product.LocalizedDescription;
					}

					if (info.ContainsKey(NSBuyProduct05Id)) {
						pricesLoaded = true;

						var product = (SKProduct) info.ObjectForKey(NSBuyProduct05Id);

						BuyItem5Btn.Enabled = true;
						Item5Title.Text = product.LocalizedTitle + " - " + product.LocalizedPrice();
						Item5Detail.Text = product.LocalizedDescription;
					}

				});

			// only if we can make payments, request the prices
			if (iap.CanMakePayments()) {
				// now go get prices, if we don't have them already
				if (!pricesLoaded)
					iap.RequestProductData(products); // async request via StoreKit -> App Store
			} else {
				// can't make payments (purchases turned off in Settings?)
				BuyItem1Btn.SetTitle ("AppStore_Disabed".Localize(), UIControlState.Disabled);
				BuyItem2Btn.SetTitle ("AppStore_Disabed".Localize(), UIControlState.Disabled);
				BuyItem3Btn.SetTitle ("AppStore_Disabed".Localize(), UIControlState.Disabled);
				BuyItem4Btn.SetTitle ("AppStore_Disabed".Localize(), UIControlState.Disabled);
				BuyItem5Btn.SetTitle ("AppStore_Disabed".Localize(), UIControlState.Disabled);
			}
				

			succeededObserver = NSNotificationCenter.DefaultCenter.AddObserver (InAppPurchaseManager.InAppPurchaseManagerTransactionSucceededNotification, 
				(notification) => {
					Server.GetCarrotCount((newCount)=> {
						UpdateTextLabels();
					});
				});
			failedObserver = NSNotificationCenter.DefaultCenter.AddObserver (InAppPurchaseManager.InAppPurchaseManagerTransactionFailedNotification, 
				(notification) => {
					// TODO: 
					Console.WriteLine ("Transaction Failed");
				});

			requestObserver = NSNotificationCenter.DefaultCenter.AddObserver (InAppPurchaseManager.InAppPurchaseManagerRequestFailedNotification, 
				(notification) => {
					// TODO: 
					Console.WriteLine ("Request Failed");
					BuyItem1Btn.SetTitle ("Request_Failed".Localize(), UIControlState.Disabled);
					BuyItem2Btn.SetTitle ("Request_Failed".Localize(), UIControlState.Disabled);
					BuyItem3Btn.SetTitle ("Request_Failed".Localize(), UIControlState.Disabled);
					BuyItem4Btn.SetTitle ("Request_Failed".Localize(), UIControlState.Disabled);
					BuyItem5Btn.SetTitle ("Request_Failed".Localize(), UIControlState.Disabled);
				});
		}

		public override void ViewWillDisappear (bool animated)
		{
			// remove the observer when the view isn't visible
			NSNotificationCenter.DefaultCenter.RemoveObserver (priceObserver);
			NSNotificationCenter.DefaultCenter.RemoveObserver (succeededObserver);
			NSNotificationCenter.DefaultCenter.RemoveObserver (failedObserver);
			NSNotificationCenter.DefaultCenter.RemoveObserver (requestObserver);

			base.ViewWillDisappear (animated);
		}

		protected HomeViewController RootController { 
			get {
				return (UIApplication.SharedApplication.Delegate as AppDelegate).RootController;
			} 
		}

		protected SidebarNavigation.SidebarController SidebarController { 
			get {
				return (UIApplication.SharedApplication.Delegate as AppDelegate).RootController.SidebarController;
			} 
		}

		// provide access to the sidebar controller to all inheriting controllers
		protected NavController NavController { 
			get {
				return (UIApplication.SharedApplication.Delegate as AppDelegate).RootController.NavController;
			} 
		}

	}
}


