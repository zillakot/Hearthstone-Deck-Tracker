﻿#region

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Input;
using Hearthstone_Deck_Tracker.Enums;
using Hearthstone_Deck_Tracker.Hearthstone;
using ListView = System.Windows.Controls.ListView;
using ListViewItem = System.Windows.Controls.ListViewItem;

#endregion

namespace Hearthstone_Deck_Tracker.Controls
{
	/// <summary>
	/// Interaction logic for NewDeckPicker.xaml
	/// </summary>
	public partial class NewDeckPicker : INotifyPropertyChanged
	{
		bool _anyArchived;
		public event PropertyChangedEventHandler PropertyChanged;

		public delegate void DoubleClickHandler(NewDeckPicker sender, Deck deck);

		public delegate void SelectedDeckHandler(NewDeckPicker sender, Deck deck);

		private readonly ObservableCollection<NewDeckPickerItem> _displayedDecks;
		private readonly ObservableCollection<DeckPickerClassItem> _classItems;
		private readonly DeckPickerClassItem _archivedClassItem;
		public bool ChangedSelection;
		private bool _ignoreSelectionChange;
		private bool _refillingList;
		private bool _reselectingDecks;
		private bool _reselectingClasses;
		private bool _clearingClasses;

		public NewDeckPicker()
		{
			InitializeComponent();
			_classItems = new ObservableCollection<DeckPickerClassItem>(
				Enum.GetValues(typeof(HeroClassAll)).OfType<HeroClassAll>().Select(x => new DeckPickerClassItem { DataContext = x }));
			_archivedClassItem = _classItems.ElementAt((int)HeroClassAll.Archived);
			ListViewClasses.ItemsSource = _classItems;
			SelectedClasses = new ObservableCollection<HeroClassAll>();
			_displayedDecks = new ObservableCollection<NewDeckPickerItem>();
			ListViewDecks.ItemsSource = _displayedDecks;
		}

		public ObservableCollection<HeroClassAll> SelectedClasses { get; private set; }

		public event SelectedDeckHandler OnSelectedDeckChanged;
		public event DoubleClickHandler OnDoubleClick;

		private void Selector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if(_reselectingClasses)
				return;

			IEnumerable<DeckPickerClassItem> removedPickerClassItems;
			var addedPickerClassItems = e.AddedItems.OfType<DeckPickerClassItem>();
			var addedClasses = PickerClassItemsAsEnum(addedPickerClassItems);
			if(addedClasses.Contains(HeroClassAll.All))
			{
				_reselectingClasses = true;
				var senderList = ((ListView)sender);
				senderList.UnselectAll();
				foreach(var item in senderList.Items)
				{
					var dpci = item as DeckPickerClassItem;
					if(dpci != null)
					{
						var hca = (HeroClassAll)dpci.DataContext;

						switch(hca)
						{
							case HeroClassAll.All:
								senderList.SelectedItems.Add(item);
								SelectPickerClassItem(dpci);
								break;
							case HeroClassAll.Archived:
								if(!SelectedClasses.Contains(HeroClassAll.Archived))
								{
									if(addedClasses.Contains(HeroClassAll.Archived))
									{
										senderList.SelectedItems.Add(item);
										SelectPickerClassItem(dpci);
									}
								}
								else
								{
									removedPickerClassItems = e.RemovedItems.OfType<DeckPickerClassItem>();
									if(PickerClassItemsAsEnum(removedPickerClassItems).Contains(HeroClassAll.Archived))
										DeselectPickerClassItem(dpci);
									else
										senderList.SelectedItems.Add(item);
								}
								break;
							default:
								DeselectPickerClassItem(dpci);
								break;
						}
					}
				}
				_reselectingClasses = false;
			}
			else
			{
				DeckPickerClassItem removedAllClassItem = null;
				removedPickerClassItems = e.RemovedItems.OfType<DeckPickerClassItem>();
				foreach(var dpci in removedPickerClassItems)
				{
					var heroClass = dpci.DataContext as HeroClassAll?;
					if(heroClass == null)
						continue;

					if(heroClass == HeroClassAll.All)
					{
						// We remove this from SelectedClasses now but we don't raise it's OnDeselected event yet,
						// instead store a reference to it in case we want to quietly add this back to the
						// SelectedClasses list later
						SelectedClasses.Remove(heroClass.Value);
						removedAllClassItem = dpci;
					}
					else
					{
						DeselectPickerClassItem(dpci);
					}
				}

				var allIsSelected = SelectedClasses.Contains(HeroClassAll.All);
				foreach(var dpci in addedPickerClassItems)
				{
					var heroClass = dpci.DataContext as HeroClassAll?;
					if(heroClass == null)
						continue;

					if(allIsSelected && heroClass != HeroClassAll.Archived)
					{
						_reselectingClasses = true;
						((ListView)sender).SelectedItems.Remove(dpci);
						_reselectingClasses = false;
						continue;
					}

					SelectPickerClassItem(dpci);
				}

				if(SelectedClasses.Count == 0 && !_clearingClasses)
				{
					var senderList = (ListView)sender;
					if(removedAllClassItem == null)
					{
						var dpciAll = PickerClassItemFromEnum(senderList, HeroClassAll.All);

						// Select 'All', raising its OnSelected event
						_reselectingClasses = true;
						senderList.SelectedItems.Add(dpciAll);
						SelectPickerClassItem(dpciAll);
						_reselectingClasses = false;
					}
					else
					{
						// If there are no selected classes, and we earlier removed 'All', quietly add it back
						_reselectingClasses = true;
						senderList.SelectedItems.Add(removedAllClassItem);
						SelectedClasses.Add(HeroClassAll.All);
						_reselectingClasses = false;

						// And make sure we do not raise its OnDeselected event if we were going to
						removedAllClassItem = null;
					}
				}
	
				// If we removed the 'All' class earlier, raise the DeckPickerClassItem's OnDeselected event now
				if(removedAllClassItem != null)
					removedAllClassItem.OnDelselected();
			}

			UpdateDecks();
		}

		private void SelectPickerClassItem(DeckPickerClassItem dpci)
		{
			var heroClass = dpci.DataContext as HeroClassAll?;
			if (heroClass != null && !SelectedClasses.Contains(heroClass.Value))
			{
				SelectedClasses.Add(heroClass.Value);
				dpci.OnSelected();
			}
		}

		private void DeselectPickerClassItem(DeckPickerClassItem dpci)
		{
			var heroClass = dpci.DataContext as HeroClassAll?;
			if(heroClass != null && SelectedClasses.Remove(heroClass.Value))
				dpci.OnDelselected();
		}


		private static IEnumerable<HeroClassAll?> PickerClassItemsAsEnum(IEnumerable<DeckPickerClassItem> items)
		{
			return items.Select(x => x.DataContext as HeroClassAll?).Where(x => x != null);
		}

		private static DeckPickerClassItem PickerClassItemFromEnum(ListView sender, HeroClassAll heroClass)
		{
			var items = sender.Items.OfType<DeckPickerClassItem>().Where(x => (x.DataContext as HeroClassAll?).HasValue);
			return items.FirstOrDefault(x => (x.DataContext as HeroClassAll?).Value == heroClass);
		}

		public void SelectClasses(List<HeroClassAll> classes)
		{
			_clearingClasses = true;
			ListViewClasses.SelectedItems.Clear();
			_clearingClasses = false;

			foreach(var item in ListViewClasses.Items)
			{
				var pickerItem = item as DeckPickerClassItem;
				if(pickerItem == null)
					continue;
				var heroClass = pickerItem.DataContext as HeroClassAll?;
				if(heroClass == null || !classes.Contains(heroClass.Value))
					continue;
				ListViewClasses.SelectedItems.Add(pickerItem);
			}
		}

		public void SelectClass(HeroClassAll heroClass)
		{
			if(!SelectedClasses.Contains(heroClass))
			{
				var dpci = PickerClassItemFromEnum(ListViewClasses, heroClass);
				ListViewClasses.SelectedItems.Add(dpci);
			}
		}

		public void UpdateDecks()
		{
			_refillingList = true;
			_displayedDecks.Clear();
			var decks =
				DeckList.Instance.Decks.Where(
				                              d =>
				                              DeckMatchesSelectedDeckType(d) && DeckMatchesSelectedTags(d)
				                              && (SelectedClasses.Any(c => ((c.ToString() == "All" || d.Class == c.ToString()) && !d.Archived) 
				                                 || (c.ToString() == "Archived" && d.Archived)))).ToList();
			foreach(var deck in decks)
				_displayedDecks.Add(new NewDeckPickerItem(deck));
			Sort();
			_refillingList = false;
			_reselectingDecks = true;
			if(decks.Contains(DeckList.Instance.ActiveDeck))
				SelectDeck(DeckList.Instance.ActiveDeck);
			_reselectingDecks = false;
		}

		public void UpdateArchivedClassVisibility()
		{
			_anyArchived = DeckList.Instance.Decks.Any(d => d.Archived);

			if(_anyArchived)
			{
				if(!_classItems.Contains(_archivedClassItem))
				{
					_classItems.Add(_archivedClassItem);
					
					if(PropertyChanged != null)
						PropertyChanged(this, new PropertyChangedEventArgs("MinWindowHeight"));
				}
			}
			else
			{
				var removed = _classItems.Remove(_archivedClassItem);

				if(removed && PropertyChanged != null)
					PropertyChanged(this, new PropertyChangedEventArgs("MinWindowHeight"));

				SelectedClasses.Remove(HeroClassAll.Archived);
				if(SelectedClasses.Count == 0)
					SelectClass(HeroClassAll.All);
			}
		}

		public double MinWindowHeight
		{
			get { return _anyArchived ? 649 : 603; }
		}

		private bool DeckMatchesSelectedDeckType(Deck deck)
		{
			if(Config.Instance.SelectedDeckType == DeckType.All)
				return true;
			return Config.Instance.SelectedDeckType == DeckType.Arena && deck.IsArenaDeck
			       || Config.Instance.SelectedDeckType == DeckType.Constructed && !deck.IsArenaDeck;
		}

		public void Sort()
		{
			var view = (CollectionView)CollectionViewSource.GetDefaultView(ListViewDecks.ItemsSource);
			view.SortDescriptions.Clear();
			view.SortDescriptions.Add(new SortDescription("Class", ListSortDirection.Ascending));

			switch(Config.Instance.SelectedDeckSorting)
			{
				case "Name":
					view.SortDescriptions.Add(new SortDescription("DeckName", ListSortDirection.Ascending));
					break;
				case "Last Edited":
					view.SortDescriptions.Add(new SortDescription("LastEdited", ListSortDirection.Descending));
					break;
				case "Tag":
					view.SortDescriptions.Add(new SortDescription("TagList", ListSortDirection.Ascending));
					break;
				case "Win Rate":
					view.SortDescriptions.Add(new SortDescription("WinPercent", ListSortDirection.Descending));
					break;
			}
		}

		private void ListViewDecks_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			foreach(var item in e.AddedItems)
			{
				var pickerItem = item as NewDeckPickerItem;
				if(pickerItem == null)
					continue;
				if(!_refillingList)
				{
					DeckList.Instance.ActiveDeck = pickerItem.DataContext as Deck;
					pickerItem.OnSelected();
					if(!_reselectingDecks && OnSelectedDeckChanged != null)
						OnSelectedDeckChanged(this, DeckList.Instance.ActiveDeck);
				}
			}
			foreach(var item in e.RemovedItems)
			{
				var pickerItem = item as NewDeckPickerItem;
				if(pickerItem == null)
					continue;
				if(!_refillingList)
					pickerItem.OnDelselected();
			}
		}

		public void SelectDeckAndAppropriateView(Deck deck)
		{
			if(Config.Instance.SelectedDeckType != DeckType.All)
			{
				if(deck.IsArenaDeck && Config.Instance.SelectedDeckType != DeckType.Arena)
					SelectDeckType(DeckType.Arena);
				else if(!deck.IsArenaDeck && Config.Instance.SelectedDeckType != DeckType.Constructed)
					SelectDeckType(DeckType.Constructed);
			}

			if(deck.Archived && !SelectedClasses.Contains(HeroClassAll.Archived))
			{
				SelectClass(HeroClassAll.Archived);
			} 
			else if(!SelectedClasses.Contains(HeroClassAll.All))
			{
				HeroClassAll deckClass;
				if(Enum.TryParse(deck.Class, out deckClass))
				{
					if(!SelectedClasses.Contains(deckClass))
						SelectClass(deckClass);
				}
			}

			UpdateDecks();
			SelectDeck(deck);
		}

		public void SelectDeck(Deck deck)
		{
			if(deck == null)
				return;
			ChangedSelection = true;
			var dpi = _displayedDecks.FirstOrDefault(x => Equals(x.Deck, deck));
			if(ListViewDecks.SelectedItem == dpi)
				return;
			if(dpi == null)
			{
				if(deck.Archived)
				{
					SelectClass(HeroClassAll.Archived);
				}
				else
				{
					HeroClassAll heroClass;
					if(Enum.TryParse(deck.Class, out heroClass))
					{
						SelectClass(heroClass);
					}
				}

				UpdateDecks();
				dpi = _displayedDecks.FirstOrDefault(x => Equals(x.Deck, deck));
				if(dpi == null)
					return;
			}
			ListViewDecks.SelectedItem = dpi;
			DeckList.Instance.ActiveDeck = deck;
			ChangedSelection = false;
		}

		public void DeselectDeck()
		{
			ListViewDecks.SelectedItem = null;
		}

		private bool DeckMatchesSelectedTags(Deck deck)
		{
			var selectedTags = Config.Instance.SelectedTags;
			return selectedTags.Any(t => t == "All")
			       || (Config.Instance.TagOperation == TagFilerOperation.Or
				           ? selectedTags.Any(t => deck.Tags.Contains(t) || t == "None" && deck.Tags.Count == 0)
				           : selectedTags.All(t => deck.Tags.Contains(t) || t == "None" && deck.Tags.Count == 0));
		}

		private async void ListViewDecks_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			if(OnDoubleClick != null)
			{
				//wait for doubleclick to be over to not reselect the deck
				await Task.Delay(SystemInformation.DoubleClickTime);
				OnDoubleClick(this, DeckList.Instance.ActiveDeck);
			}
		}

		private void ListViewDeckType_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if(_ignoreSelectionChange)
				return;
			if(e.AddedItems.Count == 0)
				Config.Instance.SelectedDeckType = DeckType.All;
			else
			{
				var item = e.AddedItems[0] as ListViewItem;
				if(item != null)
				{
					switch(item.Name)
					{
						case "ListViewItemAll":
							Config.Instance.SelectedDeckType = DeckType.All;
							break;
						case "ListViewItemArena":
							Config.Instance.SelectedDeckType = DeckType.Arena;
							break;
						case "ListViewItemConstructed":
							Config.Instance.SelectedDeckType = DeckType.Constructed;
							break;
					}
				}
				Config.Save();
				UpdateDecks();
			}
		}

		public void SelectDeckType(DeckType selectedDeckType, bool ignoreSelectionChange = false)
		{
			if(ignoreSelectionChange)
				_ignoreSelectionChange = true;
			ListViewDeckType.SelectedIndex = (int)selectedDeckType;
			if(ignoreSelectionChange)
				_ignoreSelectionChange = false;
		}
	}
}