﻿using System;
using System.Collections.Generic;
using ServiceStack.Text;
using System.IO;

namespace Fluffimax.Core
{
	public class Game
	{
		public static int kInitialCarrots = 250;
		public static int kBreedChance = 100;

		private static Player _player;
		private static List<Bunny>	_bunnyStore = new List<Bunny> ();
		public static Random Rnd = new Random();
		public static Boolean IsNewGame { get; set; }



		public static List<Bunny> BunnyStore {
			get { return _bunnyStore; }
			set { _bunnyStore = value; }
		}

		public static Player CurrentPlayer {
			get { return _player; }
		}

		public static void InitBunnyStore() {
			Server.FetchStore((storeList) => {
				if (storeList != null) {
					BunnyStore = storeList;
				} else {
					for (int i = 0; i < 20; i++) {
						BunnyStore.Add (Bunny.MakeRandomBunny ());
					}
				}});
		}

		public static void InitGameForNewPlayer() {
			Server.CreatePlayer ((newPlayer) => {
				if (newPlayer != null)
					_player = newPlayer;
				else {
					// cannot load it - create a local one
					_player = new Player ();
					Bunny startBunny = Bunny.MakeRandomBunny ();
					_player.GetBunny (startBunny);
					DateTime theDate = Today;
					_player.LastAwardDate = theDate;
					_player.RepeatPlayList = new List<DateTime> ();
					_player.FromServer = false;
				}
			});
		}

		public static DateTime Today {
			get {
				DateTime now = DateTime.Now;
				DateTime theDate = new DateTime (now.Year, now.Month, now.Day);
				return theDate;
			}
		}

		public static bool LoadExistingPlayer() {
			bool didIt = false;

			string filePath = Path.Combine (
				Environment.GetFolderPath (Environment.SpecialFolder.Personal), "BunnyPlayer.json");

			if (File.Exists(filePath)) {
				string fileText = File.ReadAllText(filePath);
				_player = fileText.FromJson<Player>();
				if (_player != null) {
					Server.LoadPlayer (CurrentPlayer.ID, (serverCopy) => {
						if (serverCopy != null) {
							_player = serverCopy;
						}
					});

					didIt = true;
				}
			}

			return didIt;
		}

		public static string MaybeRewardPlayer() {
			string rewardStr = null;

			DateTime curDate = Today;
			if (_player.LastAwardDate < curDate) {
				int daysPlayed = 0;
				// player is eligible for an award
				_player.RepeatPlayList.Add(curDate);
				if (_player.RepeatPlayList.Count > 10)
					_player.RepeatPlayList.RemoveAt (0);
				foreach (DateTime lastDate in _player.RepeatPlayList) {
					TimeSpan daysPast = curDate - lastDate;
					if (daysPast.TotalDays < 10)
						daysPlayed++;
				}
				int totalReward = _player.Bunnies.Count * daysPlayed;
				_player.CarrotCount += totalReward;
				_player.LastAwardDate = curDate;

				rewardStr = string.Format ("You've played on {0} of the last 10 days.  You've earned {1} carrots - {0} for each bunny!", daysPlayed, totalReward);

			}
			return rewardStr;
		}

		public static bool MaybeStarveBunnies() {
			bool atLeastOne = false;
			DateTime todayDate = Today;

			foreach (Bunny curBunny in CurrentPlayer.Bunnies) {
				TimeSpan feedSpan = todayDate - curBunny.LastFeedDate;
				int daysStarved = (int)Math.Truncate (feedSpan.TotalDays);
				if (daysStarved > 0) {
					curBunny.StarveBunny (daysStarved);
					atLeastOne = true;
				}
			}

			return atLeastOne;
		}

		public static bool SavePlayer() {
			bool didIt = false;
			string jsonString = CurrentPlayer.ToJson ();
			// save it
			string filePath = Path.Combine (
				Environment.GetFolderPath (Environment.SpecialFolder.Personal), "BunnyPlayer.json");
			System.IO.File.WriteAllText (filePath, jsonString);

			Server.SavePlayer(CurrentPlayer, (savedPlayer) => {
				//todo: save player
			});

			return true;
		}
	}


}

