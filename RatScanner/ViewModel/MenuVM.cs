﻿using RatScanner.Scan;
using RatScanner.TarkovDev.GraphQL;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Web;

namespace RatScanner.ViewModel;

internal class MenuVM : INotifyPropertyChanged {
	private RatScannerMain _dataSource;

	public RatScannerMain DataSource {
		get => _dataSource;
		set {
			_dataSource = value;
			OnPropertyChanged();
		}
	}

	public ItemQueue ItemScans => DataSource.ItemScans;

	public ItemScan? LastItemScan => ItemScans.LastOrDefault();

	public Item? LastItem => LastItemScan?.Item;

	public string DiscordLink => ApiManager.GetResource(ApiManager.ResourceType.DiscordLink);

	public string GithubLink => ApiManager.GetResource(ApiManager.ResourceType.GithubLink);

	public string PatreonLink => ApiManager.GetResource(ApiManager.ResourceType.PatreonLink);

	public string Updated => LastItem?.Updated != null
		? DateTime.Parse(LastItem.Updated).ToLocalTime().ToString(CultureInfo.CurrentCulture)
		: string.Empty;

	public string WikiLink {
		get {
			if (LastItem == null) return string.Empty;
			string? link = LastItem.WikiLink;
			if (link?.Length > 3) return link;
			return $"https://escapefromtarkov.gamepedia.com/{HttpUtility.UrlEncode(LastItem.Name.Replace(" ", "_"))}";
		}
	}

	public int PricePerSlot => LastItem?.GetAvg24hMarketPricePerSlot() ?? 0;

	public ItemPrice? BestTraderOffer => LastItem?.GetBestTraderOffer();
	public TraderOffer? BestTraderOfferVendor => LastItem?.GetBestTraderOfferVendor();

	public int TaskRemaining => LastItem?.GetTaskRemaining() ?? 0;

	public int HideoutRemaining => LastItem?.GetHideoutRemaining() ?? 0;

	public bool ItemNeeded => LastItem != null && (TaskRemaining + HideoutRemaining > 0);

	public List<KeyValuePair<string, KeyValuePair<int, int>>>? ItemTeamNeeds {
		get {
			if (!RatConfig.Tracking.TarkovTracker.Enable) return null;
			List<FetchModels.TarkovTracker.UserProgress> progress = RatScannerMain.Instance.TarkovTrackerDB.Progress;
			IEnumerable<FetchModels.TarkovTracker.UserProgress> teamProgress = progress.Where(x => x.UserId != RatScannerMain.Instance.TarkovTrackerDB.Self);

			List<KeyValuePair<string, KeyValuePair<int, int>>> needs = new();
			foreach (FetchModels.TarkovTracker.UserProgress? memberProgress in teamProgress) {
				if (LastItem == null) continue;
				int task = LastItem.GetTaskRemaining(memberProgress);
				int hideout = LastItem.GetHideoutRemaining(memberProgress);

				if (task == 0 && hideout == 0) continue;

				KeyValuePair<int, int> need = new(task, hideout);

				string name = memberProgress.DisplayName ?? "Unknown";
				for (int i = 2; i < 99; i++) {
					if (needs.All(n => n.Key != name)) break;
					name = $"{memberProgress.DisplayName} #{i}";
				}

				needs.Add(new KeyValuePair<string, KeyValuePair<int, int>>(name, need));
			}

			return needs;
		}
	}

	public (int task, int hideout) ItemTeamNeedsSummed => (ItemTeamNeeds?.Sum(i => i.Value.Key) ?? 0, ItemTeamNeeds?.Sum(i => i.Value.Value) ?? 0);

	public bool ItemTeamNeeded => ItemTeamNeeds != null && ItemTeamNeeds.Any();

	public event PropertyChangedEventHandler PropertyChanged;

	public MenuVM(RatScannerMain ratScanner) {
		DataSource = ratScanner;
		DataSource.PropertyChanged += ModelPropertyChanged;
	}

	protected virtual void OnPropertyChanged(string propertyName = null) {
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	public void ModelPropertyChanged(object? sender, PropertyChangedEventArgs e) {
		OnPropertyChanged();
	}

	// Still used in minimal menu
	public string IntToLongPrice(int? value) {
		if (value == null) return "0 ₽";

		string text = $"{value:n0}";
		string numberGroupSeparator = NumberFormatInfo.CurrentInfo.NumberGroupSeparator;
		return text.Replace(numberGroupSeparator, RatConfig.ToolTip.DigitGroupingSymbol) + " ₽";
	}
}
