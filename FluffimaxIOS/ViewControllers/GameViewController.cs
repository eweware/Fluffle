﻿using System;
using System.Collections.Generic;
using UIKit;
using CoreGraphics;
using Fluffimax.Core;
using System.Timers;
using ZXing.Mobile;

namespace Fluffimax.iOS
{
	public partial class GameViewController : UIViewController
	{
		private bool givingCarrot = false;
		private List<BunnyGraphic> _bunnyGraphicList = new List<BunnyGraphic> ();
		private static int _bunBaseSize = 48;
		private static int _bunSizePerLevel = 8;
		private static int kBunnyHopChance = 100;
		private static int kVerticalHopMin = 4;
		private static int kHorizontalHopMin = 8;
		private static int kVerticalHopMax = 16;
		private static int kHorizontalHopMax = 32;
		private static int kMinEventTime = 10000;
		private static double kCarrotGrowth = 2; 
		private static int kMaxEventTime = 50000;
		private static nfloat kMinWidth = -100;
		private static nfloat kMinHeight = -100;
		private static nfloat kMaxWidth = 100;
		private static nfloat kMaxHeight = 100;
		private Bunny _currentBuns = null;
		private bool inited = false;
		private Timer _idleTimer = new Timer ();
		private Timer _eventTimer = new Timer();
		private bool bunsPositioned = false;

		private class BunnyGraphic
		{
			public Bunny LinkedBuns { get; set;}
			public ShapedImageView Button { get; set;}
			public NSLayoutConstraint Width { get; set;}
			public NSLayoutConstraint Height {get; set;}
			public NSLayoutConstraint Horizontal {get; set;}
			public NSLayoutConstraint Vertical {get; set;}
			public int BunnyState { get; set;}
		}

		public GameViewController () : base ("GameViewController", null)
		{
		}

		public override void ViewDidLoad ()
		{
			base.ViewDidLoad ();
			// Perform any additional setup after loading the view, typically from a nib

			UITapGestureRecognizer tapBunnyRecognizer = new UITapGestureRecognizer ();
			tapBunnyRecognizer.NumberOfTapsRequired = 1;
			tapBunnyRecognizer.AddTarget(() => {
				HandleBunnyTap(tapBunnyRecognizer);
			});
			PlayfieldView.AddGestureRecognizer (tapBunnyRecognizer);

			UISwipeGestureRecognizer swipeRecognizer = new UISwipeGestureRecognizer();
			swipeRecognizer.Direction = UISwipeGestureRecognizerDirection.Down;
			swipeRecognizer.AddTarget(() =>
			{
				HandleBunnySwipe(swipeRecognizer);
			});
			PlayfieldView.AddGestureRecognizer(swipeRecognizer);

			BunnyDetailView.Hidden = true;

			FeedBunnyBtn.TouchUpInside += (object sender, EventArgs e) => {
				if (_currentBuns != null)
					MaybeGiveCarrot(_currentBuns);
			};

			SellBunnyBtn.TouchUpInside += (object sender, EventArgs e) => {
				MaybeSellBunny();
			};

			BuyBunnyBtn.TouchUpInside += (object sender, EventArgs e) => {
				if (Game.CurrentPlayer.Bunnies.Count < 50)
					NavController.PushViewController(new BunnyShopViewController(), true);
				else {
					HomeViewController.ShowMessageBox("Adoption_Agency".Localize(), "Too_Many_Bunnies".Localize(), "Too_Many_Bunnies_Btn".Localize());
				}
			};

			BuyCarrotsBtn.TouchUpInside += (object sender, EventArgs e) => {
				NavController.PushViewController(new CarrotShopViewController(), true);
			};

			UITapGestureRecognizer renameTap = new UITapGestureRecognizer (() => {
				ShowRenameBunny();
			});
			renameTap.NumberOfTapsRequired = 1;

			BunnyNameLabel.AddGestureRecognizer (renameTap);

			GiveBtn.TouchUpInside += (object sender, EventArgs e) => {
				Game.BunnyBeingSold = _currentBuns;
				Game.BunnySellPrice = 0;
				NavigationController.PushViewController (new GiveBunnyViewController (), true);
			};

			CatchBtn.TouchUpInside += (object sender, EventArgs e) => {
				DoCatchBunny();

			};


			// add the menu controller
			UIBarButtonItem menuBtn = new UIBarButtonItem(UIImage.FromBundle("menu-48"), UIBarButtonItemStyle.Plain, null);
			this.NavigationItem.SetLeftBarButtonItem(menuBtn, false);

			menuBtn.Clicked += (object sender, EventArgs e) =>
			{
				SidebarController.ToggleMenu();
			};

			// maybe make things smaller
			if (AppDelegate.IsMini)
			{
				// make things smaller
				UIFont smallFont = UIFont.FromName(BuyBunnyBtn.Font.Name, 10);
				BuyBunnyBtn.Font = smallFont;
				BuyCarrotsBtn.Font = smallFont;
				CatchBtn.Font = smallFont;
				View.LayoutIfNeeded();
			}

			//CatchBtn.Superview.Layer.ZPosition = 1000;
		}

		private void MaybeSellBunny() {
			Server.GetMarketPrice (_currentBuns.id, (thePrice) => {
				InvokeOnMainThread (() => {
					UIAlertView alert = new UIAlertView ();
					alert.Title = "Sell_Bunny_Title".Localize();
					alert.AddButton ("Sell_Bunny_Btn".Localize());
					alert.AddButton ("Sell_Bunny_Cancel".Localize());
					alert.Message = string.Format ("Sell_Bunny_Msg".Localize(), _currentBuns.BunnyName, thePrice);
					alert.AlertViewStyle = UIAlertViewStyle.Default;
					alert.Clicked += (object s, UIButtonEventArgs ev) => {
						if (ev.ButtonIndex == 0) {
							Server.SellBunny (_currentBuns.id, (salePrice) => {
								if (salePrice > 0) {
									InvokeOnMainThread (() => {
										Bunny soldBuns = _currentBuns;
										Game.CurrentPlayer.carrotCount += salePrice;
										SetCurrentBunny (null);
										RemoveBunnyFromPlayer (soldBuns);
										UpdateScore();

									});
								} else {
									// sell failed for some reason
									HomeViewController.ShowMessageBox("Bunny_Sale_Failed_Title".Localize(), "Bunny_Sale_Failed_Msg".Localize(), "Bunny_Sale_Failed_Btn".Localize());
								}
							});
						}
					};

					alert.Show ();
				});
			});
		}

		private async void DoCatchBunny()
		{
			var scanner = new ZXing.Mobile.MobileBarcodeScanner();
			var options = new ZXing.Mobile.MobileBarcodeScanningOptions();
			options.PossibleFormats = new List<ZXing.BarcodeFormat>() { 
				ZXing.BarcodeFormat.AZTEC 
			};
			options.CameraResolutionSelector = (resList) => {
				CameraResolution finalRes = null;

				foreach( CameraResolution curRes in resList) {
					if (((curRes.Height == 640) || (curRes.Width == 640)) && finalRes == null)
						finalRes = curRes;
					else if ((curRes.Height == 720) || (curRes.Width == 720))
						finalRes = curRes;

				}

				return finalRes;
			};

			var result = await scanner.Scan(options, false);

			if (result != null) {
				FinalizeCatch (result.Text);
			}
		}

		private void FinalizeCatch(string catchResult) {
			long tossId = long.Parse (catchResult);

			Server.GetTossStatus (tossId, (theToss) => {
				InvokeOnMainThread (() => {
					if (!theToss.isValid) {
						HomeViewController.ShowMessageBox("Catch_Failed_Title".Localize(), "Catch_Failed_Gone_Msg".Localize() , "Catch_Failed_Gone_Btn".Localize());
					} else if (theToss.price > Game.CurrentPlayer.carrotCount) {
						HomeViewController.ShowMessageBox("Catch_Failed_Title".Localize(), "Catch_Failed_Funds_Msg", "Catch_Failed_Funds_Btn");
					} else {
						UIAlertView alert = new UIAlertView();
						alert.Title = "Catch_Title".Localize();

						if (theToss.price > 0) {
							alert.Message = string.Format("Catch_Paid_Msg".Localize() , theToss.price);
							alert.AddButton("Catch_Paid_OK_Btn".Localize());
							alert.AddButton("Catch_Cancel_Btn".Localize());
						} else {
							alert.Message = "Catch_Free_Msg".Localize();
							alert.AddButton("Catch_Free_OK_Btn".Localize());
						}
						alert.AlertViewStyle = UIAlertViewStyle.Default;
						alert.Clicked += (object s, UIButtonEventArgs ev) =>
						{
							if (ev.ButtonIndex == 0) {
								// buy that bunny!
								Server.CatchToss(tossId, (theBuns) => {
									InvokeOnMainThread(() => {
									if (theBuns != null) {
											HomeViewController.ShowMessageBox("Catch_Success_Title".Localize(), "Catch_Success_Msg".Localize(), "Catch_Success_Btn".Localize());
										Game.RecentlyPurchased = true;
										Game.CurrentPlayer.Bunnies.Add(theBuns);
										CheckForNewBunnies();
									} else {
										// something went wrong
										HomeViewController.ShowMessageBox("Catch_Failed_Title".Localize(), "Catch_Failed_Unknown_Msg", "Catch_Failed_Unknown_Btn");
									}
									});
								});

							}
						};

						alert.Show ();
					}


					});

			});
		}



		private void HandleBunnyTap(UITapGestureRecognizer recognizer) {
			CGPoint theLoc = recognizer.LocationInView (PlayfieldView);
			Bunny theBuns = null;
			ShapedImageView bunsView = null;

			foreach (BunnyGraphic curGraphic in _bunnyGraphicList)
			{
				if (curGraphic.Button.Frame.Contains(theLoc))
				{
					theBuns = curGraphic.LinkedBuns;
					bunsView = curGraphic.Button;
					break;
				}
			}

			if (theBuns == null) {
				SetCurrentBunny (null);
				// see if any carrots were clicked
				UIImageView theCarrot = null;

				foreach (UIView curView in PlayfieldView.Subviews)
				{
					if (curView.Tag == 12345 && curView.Frame.Contains(theLoc))
					{
						theCarrot = curView as UIImageView;
						break;
					}
				}

				if (theCarrot != null)
				{
					PlayerGetsCarrot(theCarrot);
				}
			} 
			else 
			{
				if (theBuns == this._currentBuns)	
					DoPetBunny();
				
				SetCurrentBunny (theBuns);
			}
		}

		public override void MotionEnded(UIEventSubtype motion, UIEvent evt)
		{
			if (motion == UIEventSubtype.MotionShake)
			{
				HandleShake();
			}
			base.MotionEnded(motion, evt);
		}

		private void HandleShake()
		{
			// do something here, like rearrange all the bunnies
			foreach (BunnyGraphic curBun in _bunnyGraphicList)
			{
				int newX = Game.Rnd.Next(180) - 90;
				int newY = Game.Rnd.Next(180) - 90;
				curBun.LinkedBuns.HorizontalLoc = newX;
				curBun.LinkedBuns.VerticalLoc = newY;
				BunnyMoveToNewLoc(curBun);
			}
		}

		private void PlayerGetsCarrot(UIImageView theCarrot)
		{
			InvokeOnMainThread(() =>
			{
				UIView.Animate(.25, () =>
				{
					theCarrot.Layer.Opacity = 0;
				}, () =>
				{
					InvokeOnMainThread(() =>
					{
						Game.CurrentPlayer.GiveCarrots(1);
						theCarrot.RemoveFromSuperview();
						UpdateScore();
						theCarrot.Image = null;
					});
				});
			});
		}

		private void HandleBunnySwipe(UISwipeGestureRecognizer recognizer)
		{
			CGPoint theLoc = recognizer.LocationInView(PlayfieldView);
			Bunny theBuns = null;
			ShapedImageView bunsView = null;

			foreach (BunnyGraphic curGraphic in _bunnyGraphicList)
			{
				if (curGraphic.Button.Frame.Contains(theLoc))
				{
					theBuns = curGraphic.LinkedBuns;
					bunsView = curGraphic.Button;
					break;
				}
			}

			if ((theBuns != null) && (theBuns == this._currentBuns))
				DoPetBunny();
		}


		public void DoPetBunny()
		{
			if (bunsPositioned)
				HappyBuns(this._currentBuns);
		}

		public void HappyBuns(Bunny whichBuns)
		{
			bool superHappy = whichBuns.IncrementHappiness();
			Server.RecordPetBunny(whichBuns);

			BunnyGraphic theGraphic = _bunnyGraphicList.Find(b => b.LinkedBuns == whichBuns);
			nfloat baseX = theGraphic.Horizontal.Constant;
			nfloat baseY = theGraphic.Vertical.Constant;

			UIImageView heartImage = new UIImageView(new CGRect(100,100,24,24));
			heartImage.Image = UIImage.FromBundle("heart");
			PlayfieldView.AddSubview(heartImage);
			heartImage.TranslatesAutoresizingMaskIntoConstraints = false;
			NSLayoutConstraint csWidth = NSLayoutConstraint.Create(heartImage, NSLayoutAttribute.Width, NSLayoutRelation.Equal,
											null, NSLayoutAttribute.NoAttribute, 1, 24);
			csWidth.Active = true;

			NSLayoutConstraint csHeight = NSLayoutConstraint.Create(heartImage, NSLayoutAttribute.Height, NSLayoutRelation.Equal,
				null, NSLayoutAttribute.NoAttribute, 1, 24);
			csHeight.Active = true;
			NSLayoutConstraint csHorizontal = NSLayoutConstraint.Create(heartImage, NSLayoutAttribute.CenterX, NSLayoutRelation.Equal,
				PlayfieldView, NSLayoutAttribute.Left, 1, baseX);
			csHorizontal.Active = true;
			NSLayoutConstraint csVertical = NSLayoutConstraint.Create(heartImage, NSLayoutAttribute.CenterY, NSLayoutRelation.Equal,
				PlayfieldView, NSLayoutAttribute.Top, 1, baseY);
			csVertical.Active = true;
			heartImage.AddConstraint(csHeight);
			heartImage.AddConstraint(csWidth);
			PlayfieldView.AddConstraint(csHorizontal);
			PlayfieldView.AddConstraint(csVertical);
			heartImage.UpdateConstraints();
			PlayfieldView.UpdateConstraints();

			heartImage.Hidden = false;
			heartImage.Layer.Opacity = 1;
			heartImage.Layer.ZPosition = 10000;
			PlayfieldView.LayoutIfNeeded();

			UIView.Animate(1, () =>
			{
				csVertical.Constant = baseY - 128;
				heartImage.Layer.Opacity = 0;
				csHeight.Constant = 64;
				csWidth.Constant = 64;
				PlayfieldView.LayoutIfNeeded();
			}, () =>
			{
				InvokeOnMainThread(() =>
				{
					heartImage.Hidden = true;
					heartImage.Layer.Opacity = 1;
					heartImage.RemoveFromSuperview();
					PlayfieldView.RemoveConstraint(csHorizontal);
					PlayfieldView.RemoveConstraint(csVertical);
					heartImage.RemoveConstraint(csWidth);
					heartImage.RemoveConstraint(csHeight);
					heartImage.Dispose();
				});
			});


		}

		public void ShowRenameBunny() {
			if (string.IsNullOrEmpty(_currentBuns.BunnyName) || _currentBuns.OriginalOwner == Game.CurrentPlayer.id) {
				UIAlertView alert = new UIAlertView ();
				alert.Title = "Rename_Title".Localize();
				alert.AddButton ("ok_btn".Localize());
				alert.AddButton ("cancel_btn".Localize());
				alert.Message = "Rename_Msg".Localize();
				alert.AlertViewStyle = UIAlertViewStyle.PlainTextInput;
				alert.Clicked += (object s, UIButtonEventArgs ev) => {
					if (ev.ButtonIndex == 0) {
						string input = alert.GetTextField (0).Text;
						_currentBuns.BunnyName = input;
						Server.RecordRenameBunny(_currentBuns);
						UpdateBunnyPanel ();
					}
				};
				alert.Show ();
			} else {
				HomeViewController.ShowMessageBox ("Rename_Title".Localize(), "Rename_Err_Msg".Localize(), "Rename_Err_Btn".Localize());
			}

		}

		public override void ViewWillAppear (bool animated)
		{
			base.ViewWillAppear (animated);

			this.NavigationController.NavigationBar.TitleTextAttributes = new UIStringAttributes()
			{
				Font = UIFont.FromName("FingerPaint-Regular", 20),
				ForegroundColor = UIColor.FromRGB(0, 200, 0)
			};

			UIBarButtonItem menuBtn = new UIBarButtonItem("back".Localize(), UIBarButtonItemStyle.Bordered, null);
			this.NavigationItem.BackBarButtonItem = menuBtn;

			BunnyNameLabel.UserInteractionEnabled = true;
			GrassField.UserInteractionEnabled = true;
			NavController.NavigationBarHidden = false;

			InitGame();

			UpdateScore ();

			CheckForNewBunnies ();
			CheckForRecentPurchase ();
		}






		private void CheckForNewBunnies() {
			if (Game.RecentlyPurchased) {
				// more bunnies have been bought - add them if needed
				foreach (Bunny curBunny in Game.CurrentPlayer.Bunnies) {
					if (_bunnyGraphicList.Find (b => b.LinkedBuns == curBunny) == null) {
						AddBunnyToScreen (curBunny);
					}
				}
				Game.RecentlyPurchased = false;
			}
		}

		private void CheckForRecentPurchase() {
			if (Game.BunnyBeingSold != null) {
				SetCurrentBunny (null);
				RemoveBunnyFromPlayer (Game.BunnyBeingSold);
				Game.BunnyBeingSold = null;
			}
		}

		private void RemoveBunnyFromPlayer(Bunny theBuns) {
			Game.CurrentPlayer.Bunnies.Remove (theBuns);
			BunnyGraphic theGraphic = _bunnyGraphicList.Find (b => b.LinkedBuns == theBuns);
			theGraphic.Button.RemoveFromSuperview ();
			_bunnyGraphicList.Remove (theGraphic);
		}





		private ShapedImageView AddBunnyToScreen(Bunny thebuns) {
			ShapedImageView bunsBtn = new ShapedImageView ();

			PlayfieldView.AddSubview (bunsBtn);
			bunsBtn.TranslatesAutoresizingMaskIntoConstraints = false;
			UIImage[]	imgList = SpriteManager.GetImageList (thebuns, "idle", "front");
			bunsBtn.AnimationImages = imgList;
			bunsBtn.AnimationDuration = 1;
			bunsBtn.UserInteractionEnabled = true;

			NSLayoutConstraint csWidth = NSLayoutConstraint.Create (bunsBtn, NSLayoutAttribute.Width, NSLayoutRelation.Equal,
				                            null, NSLayoutAttribute.NoAttribute, 1, 32);
			csWidth.Active = true;

			NSLayoutConstraint csHeight = NSLayoutConstraint.Create (bunsBtn, NSLayoutAttribute.Height, NSLayoutRelation.Equal,
				null, NSLayoutAttribute.NoAttribute, 1, 32);
			csHeight.Active = true;
			NSLayoutConstraint csHorizontal = NSLayoutConstraint.Create (bunsBtn, NSLayoutAttribute.CenterX, NSLayoutRelation.Equal,
				PlayfieldView, NSLayoutAttribute.Left, 1, 0);
			csHorizontal.Active = true;
			NSLayoutConstraint csVertical = NSLayoutConstraint.Create (bunsBtn, NSLayoutAttribute.CenterY, NSLayoutRelation.Equal,
				PlayfieldView, NSLayoutAttribute.Top, 1, 0);
			csVertical.Active = true;

			PlayfieldView.AddConstraint (csHorizontal);
			PlayfieldView.AddConstraint (csVertical);

			bunsBtn.AddConstraint (csWidth);
			bunsBtn.AddConstraint (csHeight);
			bunsBtn.UpdateConstraints ();


			BunnyGraphic graphic = new BunnyGraphic ();
			graphic.BunnyState = 1;
			graphic.Button = bunsBtn;
			graphic.Height = csHeight;
			graphic.Width = csWidth;
			graphic.Horizontal = csHorizontal;
			graphic.Vertical = csVertical;
			graphic.LinkedBuns = thebuns;
			_bunnyGraphicList.Add (graphic);


			// todo:  add a gesture recognizer  
			//bunsBtn.TouchUpInside += HandleBunnyClick;
			UpdateBunsSizeAndLocation(thebuns);
			PlayfieldView.UpdateConstraints ();

			PlayfieldView.BringSubviewToFront (bunsBtn);
			bunsBtn.StartAnimating();
			PlayfieldView.LayoutIfNeeded();
			DoBunnyHop(graphic);
			return bunsBtn;
		}

		private void UpdateBunsSizeAndLocation(Bunny thebuns) {
			BunnyGraphic theGraphic = _bunnyGraphicList.Find (b => b.LinkedBuns == thebuns);

			if (theGraphic != null) {
				InvokeOnMainThread (() => {
					CGRect bounds = PlayfieldView.Frame;
					nfloat xScale = bounds.Width / 200;
					nfloat yScale = bounds.Height / 200;
					nfloat newY = (thebuns.VerticalLoc + 100) * yScale;
					nfloat bunsSizeBase = (nfloat)BunnySizeForLevel (thebuns.BunnySize);
					double nextLevelSize = BunnySizeForLevel (thebuns.BunnySize + 1);
					nfloat deltaSize = (nfloat)((nextLevelSize - bunsSizeBase) * thebuns.Progress);
					theGraphic.Button.Layer.ZPosition = 200 + newY;
					nfloat scale = 0.5f + 0.4f * (((float)thebuns.VerticalLoc + 100) / (float)200);

					theGraphic.Button.Transform = CGAffineTransform.MakeScale(scale, scale);
					UIView.Animate(.1, () =>
					{
						theGraphic.Height.Constant = bunsSizeBase;
						theGraphic.Width.Constant = bunsSizeBase + deltaSize;
						theGraphic.Horizontal.Constant = (thebuns.HorizontalLoc + 100) * xScale;
						theGraphic.Vertical.Constant =  newY;


						PlayfieldView.LayoutIfNeeded();
					}, null);

				});
			}
		}

		public override void ViewWillDisappear(bool animated)
		{
			base.ViewWillDisappear(animated);
			Game.CurrentPlayer.SaveBunnies();
			_idleTimer.Stop();
			_eventTimer.Stop();
		}

		public override void ViewDidAppear(bool animated)
		{
			base.ViewDidAppear(animated);

			if (!bunsPositioned)
			{
				foreach (BunnyGraphic buns in _bunnyGraphicList)
				{
					BunnyMoveToNewLoc(buns);
				}
				bunsPositioned = true;
			}

			if (inited)
			{
				_idleTimer.Start();
				_eventTimer.Start();
			}
		}


		public double BunnySizeForLevel(int level) {
			return _bunBaseSize + (_bunSizePerLevel * level);
		}

		public override void DidReceiveMemoryWarning ()
		{
			base.DidReceiveMemoryWarning ();
			// Release any cached data, images, etc that aren't in use.
		}

		private void InitGame() {
			if (!inited)
			{
				Game.NewPlayerLoaded = false;
				InvokeOnMainThread(() =>
				{
					CarrotImg.Hidden = true;
					// ad bunnies
					foreach (Bunny curBunny in Game.CurrentPlayer.Bunnies)
					{
						AddBunnyToScreen(curBunny);
					}
					HideBunnyPanel();
					UpdateScore();
					StartTimers();
					inited = true;

					if (HomeViewController.ShowTutorialStep("click_bunny_tutorial", "click_bunny_tutorial".Localize())) { }
					else if (HomeViewController.ShowTutorialStep("bunny_breed_tutorial", "bunny_breed_tutorial".Localize())) { }
					else if (HomeViewController.ShowTutorialStep("buy_carrots_tutorial", "buy_carrots_tutorial".Localize())) { }
					else if (HomeViewController.ShowTutorialStep("bunny_catch_tutorial", "bunny_catch_tutorial".Localize())) { }
					inited = true;

				});
			}
			else if (Game.NewPlayerLoaded) {
				// a new player was loaded - get rid of existing bunnies and add new ones.
				Game.NewPlayerLoaded = false;
				InvokeOnMainThread(() =>
				{
					CarrotImg.Hidden = true;
					HideBunnyPanel();
					UpdateScore();
					foreach (BunnyGraphic oldBuns in _bunnyGraphicList)
					{
						oldBuns.Button.RemoveFromSuperview();
					}
					_bunnyGraphicList.Clear();
					foreach (Bunny curBunny in Game.CurrentPlayer.Bunnies)
					{
						AddBunnyToScreen(curBunny);
					}

				});

			}
		}

		private void SetCurrentBunny(Bunny newBuns) {
			if (_currentBuns != null)
				DeselectBunny (_currentBuns);
			_currentBuns = newBuns;
			if (newBuns != null) {
				SelectBunny (newBuns);
				ShowBunnyPanel ();
			}
			else {
				HideBunnyPanel ();

			}
		}

		private void SelectBunny(Bunny theBuns) {
			InvokeOnMainThread (() => {
				UIImage[]	imgList = SpriteManager.GetImageList(theBuns, "idle", "front");
				ShapedImageView bunBtn = _bunnyGraphicList.Find(b => b.LinkedBuns == theBuns).Button;

				if (bunBtn != null) {
					View.BringSubviewToFront(bunBtn);
					bunBtn.AnimationImages = imgList;
					bunBtn.AnimationDuration = 1;
					bunBtn.StartAnimating ();
				}
			});
		}

		private void DeselectBunny(Bunny theBuns) {
			InvokeOnMainThread (() => {

			});
		}

		private void ShowBunnyPanel() {
			InvokeOnMainThread (() => {
				UpdateBunnyPanel();
				if (BunnyDetailView.Hidden) {
					BunnyDetailView.Layer.Opacity = 0;
					BunnyDetailView.Hidden = false;
					UIView.Animate (.5, () => {
						BunnyDetailView.Layer.Opacity = 1;
					}, () => {
						BunnyDetailView.Layer.Opacity = 1;
						UpdateBunnyPanel();
					});

					// tutorial
					if (HomeViewController.ShowTutorialStep("button_details_tutorial", "button_details_tutorial".Localize())) { }
					else if (HomeViewController.ShowTutorialStep("rename_tutorial", "rename_tutorial".Localize())) { }
					else if (HomeViewController.ShowTutorialStep("bunny_pet_tutorial", "bunny_pet_tutorial".Localize())) { }
					else if (HomeViewController.ShowTutorialStep("sell_bunny_tutorial", "sell_bunny_tutorial".Localize())) { }
					else if (HomeViewController.ShowTutorialStep("bunny_toss_tutorial", "bunny_toss_tutorial".Localize())) { }

				}
			});
		}

		private void UpdateBunnyPanel() {
			if (_currentBuns != null) {
				InvokeOnMainThread (() => {
					string nameStr = _currentBuns.BunnyName;
					if (string.IsNullOrEmpty(nameStr))
					{
						nameStr = "Unamed_Bunny".Localize();
						BunnyNameLabel.TextColor = UIColor.Black;
					}
					else {
						BunnyNameLabel.TextColor = UIColor.Blue;
					}
					BunnyNameLabel.Text = nameStr;
					BunnyInfoLabel.Text = _currentBuns.Description;

					ProgressIndicator.SetProgress((float)_currentBuns.FeedState / (float)_currentBuns.CarrotsForNextSize(_currentBuns.BunnySize), true);
					SizeCount.Text = String.Format("{0}/{1}", _currentBuns.FeedState,_currentBuns.CarrotsForNextSize(_currentBuns.BunnySize));
				});
			}
		}

		private void HideBunnyPanel() {
			if (!BunnyDetailView.Hidden) {
				InvokeOnMainThread (() => {
					BunnyDetailView.Layer.Opacity = 1;
					BunnyDetailView.Hidden = false;
					UIView.Animate (.25, () => {
						BunnyDetailView.Layer.Opacity = 0;
					}, () => {
						BunnyDetailView.Layer.Opacity = 0;
						BunnyDetailView.Hidden = true;
					});
				});
			}
		}

		private void StartTimers() {
			_idleTimer.Interval = 500;
			_idleTimer.AutoReset = false;
			_idleTimer.Elapsed += (object sender, ElapsedEventArgs e) => {
				MaybeBunniesHop();
			};

			_eventTimer.Interval = kMinEventTime;
			_eventTimer.AutoReset = false;
			_eventTimer.Elapsed += (sender, e) => { MaybeDoEvent(); };

			_eventTimer.Start();
			_idleTimer.Start ();
		}

		private void MaybeDoEvent()
		{
			InvokeOnMainThread(() =>
			{
				CGRect playFieldBounds = PlayfieldView.Frame;
				float x = Game.Rnd.Next((int)playFieldBounds.Width);
				float y = Game.Rnd.Next((int)playFieldBounds.Height);
				float scale = 0.5f + (0.5f * (y / (float)playFieldBounds.Height));
				float width = (float)playFieldBounds.Width / 25 * scale;
				float height = width * 2;
				y -= height;
				if (y < -width) y = -width;
				CGRect theBounds = new CGRect(x, y, width, height);
				UIImageView theView = new UIImageView(theBounds);
				theView.Tag = 12345;
				theView.Image = UIImage.FromBundle("carrotplant");
				PlayfieldView.AddSubview(theView);
				theView.Layer.ZPosition = 100 + (200 * (y / (float)playFieldBounds.Height));
				theView.Layer.Transform = CoreAnimation.CATransform3D.MakeScale(1, 0, 1);
				UIView.Animate(kCarrotGrowth, () =>
				{
					theView.Layer.Transform = CoreAnimation.CATransform3D.MakeScale(1, 1, 1);
				}, () =>
				{
					_eventTimer.Interval = kMinEventTime + Game.Rnd.Next(kMaxEventTime);
					_eventTimer.Start();
				});

			});
		}

		private void MaybeBunniesHop() {
			if ((_bunnyGraphicList.Count > 0) && (Game.Rnd.Next (100) < kBunnyHopChance)) {
				int whichBunny = Game.Rnd.Next (_bunnyGraphicList.Count);
				BunnyGraphic bunsGraphic = _bunnyGraphicList [whichBunny];
				if ((bunsGraphic.LinkedBuns == _currentBuns) && givingCarrot) {
					// don't jump when eating
					_idleTimer.Start ();
				} else 
					DoBunnyHop (bunsGraphic);

			} else {
				_idleTimer.Start ();
			}
		}

		private void DoBunnyHop(BunnyGraphic buns) {
			int dir = Game.Rnd.Next (8);
			int xDif = 0, yDif = 0;
			int verticalHop = Game.Rnd.Next (kVerticalHopMin, kVerticalHopMax);
			int horizontalHop = Game.Rnd.Next (kHorizontalHopMin, kHorizontalHopMax);
			UIImage[]  bunnyJumpImageFrames = null;
			UIImage[] bunnyIdleImageFrames = null;
			bool flip = false;

			switch (dir) {
			case 0://up
				yDif = -verticalHop;
				bunnyJumpImageFrames = SpriteManager.GetImageList (buns.LinkedBuns, "hop", "back");
				bunnyIdleImageFrames = SpriteManager.GetImageList (buns.LinkedBuns, "idle", "back");
				break;
			case 1: //upright
				yDif = -verticalHop;
				xDif = horizontalHop;
				bunnyJumpImageFrames = SpriteManager.GetImageList (buns.LinkedBuns, "hop", "rightback");
				bunnyIdleImageFrames = SpriteManager.GetImageList (buns.LinkedBuns, "idle", "rightback");
				break;
			case 2: // right
				xDif = horizontalHop;
				bunnyJumpImageFrames = SpriteManager.GetImageList (buns.LinkedBuns, "hop", "right");
				bunnyIdleImageFrames = SpriteManager.GetImageList (buns.LinkedBuns, "idle", "right");
				break;
			case 3: // downright
				yDif = verticalHop;
				xDif = horizontalHop;
				bunnyJumpImageFrames = SpriteManager.GetImageList (buns.LinkedBuns, "hop", "rightfront");
				bunnyIdleImageFrames = SpriteManager.GetImageList (buns.LinkedBuns, "idle", "rightfront");
				break;
			case 4: // down
				yDif = verticalHop;
				bunnyJumpImageFrames = SpriteManager.GetImageList (buns.LinkedBuns, "hop", "front");
				bunnyIdleImageFrames = SpriteManager.GetImageList (buns.LinkedBuns, "idle", "front");
				break;
			case 5: // downleft
				yDif = verticalHop;
				xDif = -horizontalHop;
				bunnyJumpImageFrames = SpriteManager.GetImageList (buns.LinkedBuns, "hop", "rightfront");
				bunnyIdleImageFrames = SpriteManager.GetImageList (buns.LinkedBuns, "idle", "rightfront");
				flip = true;
				break;
			case 6:// left
				xDif = -horizontalHop;
				bunnyJumpImageFrames = SpriteManager.GetImageList (buns.LinkedBuns, "hop", "right");
				bunnyIdleImageFrames = SpriteManager.GetImageList (buns.LinkedBuns, "idle", "right");
				flip = true;
				break;
			case 7: // upleft
				yDif = -verticalHop;
				xDif = -horizontalHop;
				bunnyJumpImageFrames = SpriteManager.GetImageList (buns.LinkedBuns, "hop", "rightback");
				bunnyIdleImageFrames = SpriteManager.GetImageList (buns.LinkedBuns, "idle", "rightback");
				flip = true;
				break;
			}

			if (bunnyJumpImageFrames == null || bunnyIdleImageFrames == null) {

			}


			InvokeOnMainThread (() => {
				nfloat scale = 0.5f + (0.5f * (((float)buns.LinkedBuns.VerticalLoc + 100) / 200));
				xDif = (int)((float)xDif * scale);
				yDif = (int)((float)yDif * scale);

				nfloat newX = buns.LinkedBuns.HorizontalLoc + xDif;
				nfloat newY = buns.LinkedBuns.VerticalLoc + yDif;

				if (newX < kMinWidth)
					newX = kMinWidth;
				else if (newX > kMaxWidth)
					newX = kMaxWidth;

				if (newY < kMinHeight)
					newY = kMinHeight;
				else if (newY > kMaxHeight)
					newY = kMaxHeight;

				scale = 0.5f + (0.5f * ((newY + 100) / 200));

				if (flip)
					buns.Button.Transform = CGAffineTransform.MakeScale(-scale,scale);
				else
					buns.Button.Transform = CGAffineTransform.MakeScale(scale, scale);

				BunnyHopToNewLoc(buns, dir, newX, newY, bunnyJumpImageFrames, bunnyIdleImageFrames);
			});
		}



		private void BunnyMoveToNewLoc(BunnyGraphic buns)
		{
			nfloat newX = buns.LinkedBuns.HorizontalLoc;
			nfloat newY = buns.LinkedBuns.VerticalLoc;
			CGRect bounds = PlayfieldView.Frame;
			nfloat xScale = bounds.Width / 200;
			nfloat yScale = bounds.Height / 200;

			buns.Horizontal.Constant = (newX + 100) * xScale;
			buns.Vertical.Constant = (newY + 100) * yScale;
			PlayfieldView.LayoutIfNeeded();
			buns.Button.AnimationDuration = 1 + Game.Rnd.NextDouble();
			buns.Button.StartAnimating();
			buns.Button.Layer.ZPosition = 200 + newY;
			buns.LinkedBuns.UpdateLocation((int)newX, (int)newY);
		}

		private void BunnyHopToNewLoc(BunnyGraphic buns, int dir, nfloat newX, nfloat newY, UIImage[] jumpFrames, UIImage[] idleFrames) {
			CGRect bounds = PlayfieldView.Frame;
			nfloat xScale = bounds.Width / 200;
			nfloat yScale = bounds.Height / 200;

			InvokeOnMainThread (() => {
				buns.Button.AnimationImages = jumpFrames;
				buns.Button.AnimationDuration = .15;
				buns.Button.StartAnimating ();
			});
			UIView.Animate (.5, () => {
				buns.Horizontal.Constant = (newX + 100) * xScale;
				buns.Vertical.Constant = (newY + 100) * yScale;
				PlayfieldView.LayoutIfNeeded();
			}, () => {
				InvokeOnMainThread (() => {
					buns.Button.AnimationImages = idleFrames;
					buns.Button.AnimationDuration = 1 + Game.Rnd.NextDouble();
					buns.Button.StartAnimating ();
					buns.Button.Layer.ZPosition = 200 + newY;
				});
				buns.LinkedBuns.UpdateLocation((int)newX, (int)newY);
				_idleTimer.Start();
				CheckBunnyBreeding(buns);
			});
		}

		private void CheckBunnyBreeding(BunnyGraphic firstBuns)
		{

			foreach (BunnyGraphic secondBuns in _bunnyGraphicList)
			{
				if (firstBuns != secondBuns)
				{
					if (firstBuns.Button.Frame.IntersectsWith(secondBuns.Button.Frame))
					{
						if (Bunny.BunniesCanBreed(secondBuns.LinkedBuns, firstBuns.LinkedBuns))
						{
							HappyBuns(firstBuns.LinkedBuns);
							Server.BreedBunnies(firstBuns.LinkedBuns, secondBuns.LinkedBuns, (newBuns) =>
							{
								if (newBuns != null)
								{
									firstBuns.LinkedBuns.LastBredDate = DateTime.Now;
									secondBuns.LinkedBuns.LastBredDate = DateTime.Now;
									newBuns.HorizontalLoc = firstBuns.LinkedBuns.HorizontalLoc;
									newBuns.VerticalLoc = secondBuns.LinkedBuns.VerticalLoc;
									AddBunnyToScreen(newBuns);
								}
							});
						}
					}

				}
			}
		}



		private void UpdateScore() {
			InvokeOnMainThread (() => {
				//CarrotCount.Text = Game.CurrentPlayer.carrotCount.ToString();
				this.Title = Game.CurrentPlayer.carrotCount.ToString() + " carrots";
			});
		}

		private void MaybeGiveCarrot(Bunny theBuns)
		{

			if (Game.CurrentPlayer.carrotCount > 0)
			{
				givingCarrot = true;
				// ok give one
				InvokeOnMainThread(() =>
				{
					// create a carrot image
					BunnyGraphic theGraphic = _bunnyGraphicList.Find(b => b.LinkedBuns == theBuns);
					nfloat baseX = theGraphic.Horizontal.Constant;
					nfloat baseY = theGraphic.Vertical.Constant;

					UIImageView carrotImage = new UIImageView(new CGRect(100, 100, 24, 24));
					carrotImage.Image = UIImage.FromBundle("carrot");
					PlayfieldView.AddSubview(carrotImage);
					carrotImage.TranslatesAutoresizingMaskIntoConstraints = false;
					NSLayoutConstraint csWidth = NSLayoutConstraint.Create(carrotImage, NSLayoutAttribute.Width, NSLayoutRelation.Equal,
													null, NSLayoutAttribute.NoAttribute, 1, 96);
					csWidth.Active = true;

					NSLayoutConstraint csHeight = NSLayoutConstraint.Create(carrotImage, NSLayoutAttribute.Height, NSLayoutRelation.Equal,
						null, NSLayoutAttribute.NoAttribute, 1, 96);
					csHeight.Active = true;
					NSLayoutConstraint csHorizontal = NSLayoutConstraint.Create(carrotImage, NSLayoutAttribute.CenterX, NSLayoutRelation.Equal,
						PlayfieldView, NSLayoutAttribute.Left, 1, baseX);
					csHorizontal.Active = true;
					NSLayoutConstraint csVertical = NSLayoutConstraint.Create(carrotImage, NSLayoutAttribute.CenterY, NSLayoutRelation.Equal,
						PlayfieldView, NSLayoutAttribute.Top, 1, baseY);
					csVertical.Active = true;
					carrotImage.AddConstraint(csHeight);
					carrotImage.AddConstraint(csWidth);
					PlayfieldView.AddConstraint(csHorizontal);
					PlayfieldView.AddConstraint(csVertical);
					carrotImage.UpdateConstraints();
					PlayfieldView.UpdateConstraints();

					carrotImage.Hidden = false;
					carrotImage.Layer.Opacity = 1;
					carrotImage.Layer.ZPosition = 10000;
					PlayfieldView.LayoutIfNeeded();



					UpdateScore();
					UIImage[] imageList = SpriteManager.GetImageList(theBuns, "idle", "front");
					ShapedImageView bunBtn = _bunnyGraphicList.Find(b => b.LinkedBuns == theBuns).Button;

					if (bunBtn != null)
					{
						View.BringSubviewToFront(bunBtn);
						bunBtn.AnimationImages = imageList;
						bunBtn.AnimationDuration = 1;
						bunBtn.StartAnimating();
					}


					bool grew = Game.CurrentPlayer.FeedBunny(theBuns);
					nfloat bunsSizeBase = (nfloat)BunnySizeForLevel(theBuns.BunnySize);
					double nextLevelSize = BunnySizeForLevel(theBuns.BunnySize + 1);
					nfloat deltaSize = (nfloat)((nextLevelSize - bunsSizeBase) * theBuns.Progress);
					csHorizontal.Constant = theGraphic.Horizontal.Constant;
					csVertical.Constant = 0;//theGraphic.Vertical.Constant - 100;
					csWidth.Constant = 96;
					csHeight.Constant = 96;

					carrotImage.Layer.Opacity = 1;
					double duration = 0;
					if (grew)
						duration = 4;
					PlayfieldView.LayoutIfNeeded();
					carrotImage.Layer.Transform.Rotate((nfloat)Math.PI / 4, 0, 0, 1);

					UIView.Animate(1, () =>
					{
						csWidth.Constant = 0;
						csHeight.Constant = 0;
						csVertical.Constant = theGraphic.Vertical.Constant + 24;
						//carrotImage.Layer.Opacity = 0;
						carrotImage.Layer.Transform.Rotate(0, 0, 0, 1);
						PlayfieldView.LayoutIfNeeded();
					}, () =>
					{
						InvokeOnMainThread(() =>
						{
							carrotImage.Hidden = true;
							carrotImage.Layer.Opacity = 1;
							carrotImage.RemoveFromSuperview();
							PlayfieldView.RemoveConstraint(csHorizontal);
							PlayfieldView.RemoveConstraint(csVertical);
							carrotImage.RemoveConstraint(csWidth);
							carrotImage.RemoveConstraint(csHeight);
							carrotImage.Dispose();

							UIView.Animate(duration, () =>
							{
								theGraphic.Height.Constant = bunsSizeBase;
								theGraphic.Width.Constant = bunsSizeBase + deltaSize;
								PlayfieldView.LayoutIfNeeded();
							}, () =>
							{
								InvokeOnMainThread(() =>
								{
									UpdateBunsSizeAndLocation(theBuns);
									UpdateBunnyPanel();
									givingCarrot = false;
								//FeedBunnyBtn.Enabled = true;
								if (grew)
									{
										HomeViewController.ShowTutorialStep("bunny_grow_tutorial", "bunny_grow_tutorial".Localize());
									}
								});
							});

						});
					});
				});
			}
		}

		private void AnimateBunsSizeAndLocation(Bunny thebuns, bool grew) {
			BunnyGraphic theGraphic = _bunnyGraphicList.Find (b => b.LinkedBuns == thebuns);
			View.BringSubviewToFront (CarrotImg);
			if (theGraphic != null) {
				
				nfloat bunsSizeBase = (nfloat)BunnySizeForLevel (thebuns.BunnySize);
				double nextLevelSize = BunnySizeForLevel (thebuns.BunnySize + 1);
				nfloat deltaSize = (nfloat)((nextLevelSize - bunsSizeBase) * thebuns.Progress);
				CSCarrotX.Constant = theGraphic.Horizontal.Constant;
				CSCarrotY.Constant = theGraphic.Vertical.Constant;
				CSCarrotWidth.Constant = 96;
				CSCarrotHeight.Constant = 96;

				CarrotImg.Layer.Opacity = 1;
				double duration = 0;
				if (grew)
					duration = 4;
				PlayfieldView.LayoutIfNeeded();
				
				UIView.Animate (1, () => {
					CSCarrotWidth.Constant = 0;
					CSCarrotHeight.Constant = 0;
					CarrotImg.Layer.Opacity = 0;
					PlayfieldView.LayoutIfNeeded();
				}, () => {
					InvokeOnMainThread(() => {
						CSCarrotWidth.Constant = 96;
						CSCarrotHeight.Constant = 96;
						CarrotImg.Layer.Opacity = 1;
						CarrotImg.Hidden = true;
						PlayfieldView.LayoutIfNeeded();

						UIView.Animate (duration, () => {
							theGraphic.Height.Constant = bunsSizeBase;
							theGraphic.Width.Constant = bunsSizeBase + deltaSize;
							PlayfieldView.LayoutIfNeeded();
						}, () => {
							InvokeOnMainThread(() => {
								UpdateBunsSizeAndLocation(thebuns);
								UpdateBunnyPanel(); 
								givingCarrot = false;
								//FeedBunnyBtn.Enabled = true;
								if (grew)
								{
									HomeViewController.ShowTutorialStep("bunny_grow_tutorial", "bunny_grow_tutorial".Localize());
								}
							});
						});

					});
				});


			}
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


