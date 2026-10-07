using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

using CommunityToolkit.Mvvm.Input;

namespace Bloxstrap.UI.ViewModels.Settings
{
    public class AccountRow
    {
        public long UserId { get; set; }
        public string Display { get; set; } = "";
        public string Subtitle { get; set; } = "";
        public bool IsCurrent { get; set; }
    }

    public class AccountsViewModel : NotifyPropertyChangedViewModel
    {
        private string _statusText = "";
        private AccountRow? _selectedAccount;

        public ObservableCollection<AccountRow> Accounts { get; } = new();

        public AccountRow? SelectedAccount
        {
            get => _selectedAccount;
            set
            {
                _selectedAccount = value;
                OnPropertyChanged(nameof(SelectedAccount));
                OnPropertyChanged(nameof(HasSelection));
                (SwitchCommand as CommunityToolkit.Mvvm.Input.RelayCommand)?.NotifyCanExecuteChanged();
                (DeleteCommand as CommunityToolkit.Mvvm.Input.RelayCommand)?.NotifyCanExecuteChanged();
            }
        }

        public bool HasSelection => SelectedAccount is not null;

        public string StatusText
        {
            get => _statusText;
            private set
            {
                _statusText = value;
                OnPropertyChanged(nameof(StatusText));
            }
        }

        public bool NeedsCookieAccess => !App.Cookies.Loaded;

        public string CookieStateText
        {
            get
            {
                string detail = App.Cookies.State switch
                {
                    CookieState.Success => "logged in, ready to save accounts.",
                    CookieState.NotAllowed => "account access is off. Turn it on under Behaviour (experimental).",
                    CookieState.NotFound => $"no Roblox cookie file found at {App.Cookies.CookieFilePath}. Launch the Roblox player once while logged in.",
                    CookieState.Invalid => "cookie file found but the session is invalid (logged out or expired). Log into Roblox in the player, then try again.",
                    CookieState.Failed => "cookie file could not be read. Try relaunching snitch.out.",
                    _ => "cookie state unknown yet. Wait a few seconds and reopen this page."
                };

                return $"Cookie state: {App.Cookies.State}. {detail}";
            }
        }

        public ICommand SaveCurrentCommand { get; }
        public ICommand SwitchCommand { get; }
        public ICommand DeleteCommand { get; }

        public AccountsViewModel()
        {
            SaveCurrentCommand = new RelayCommand(async () => await SaveCurrent());
            SwitchCommand = new RelayCommand(Switch, () => HasSelection);
            DeleteCommand = new RelayCommand(Delete, () => HasSelection);
            Reload();
            App.Cookies.StateChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(NeedsCookieAccess));
                OnPropertyChanged(nameof(CookieStateText));
            };
        }

        public void Reload()
        {
            Accounts.Clear();

            long? current = App.AccountVault.GetCurrentUserId();

            foreach (var account in App.AccountVault.Accounts.OrderBy(x => x.DisplayName))
            {
                bool isCurrent = current == account.UserId;

                Accounts.Add(new AccountRow
                {
                    UserId = account.UserId,
                    Display = String.IsNullOrEmpty(account.DisplayName) ? account.Username : $"{account.DisplayName} (@{account.Username})",
                    Subtitle = $"Added {account.AddedUtc.ToLocalTime():g}{(isCurrent ? " · current" : "")}",
                    IsCurrent = isCurrent
                });
            }

            OnPropertyChanged(nameof(Accounts));
        }

        private async Task SaveCurrent()
        {
            string? error = await App.AccountVault.SaveCurrentAsync();
            StatusText = error ?? "Current account saved.";
            Reload();
        }

        private void Switch()
        {
            if (SelectedAccount is null)
                return;

            string? error = App.AccountVault.SwitchTo(SelectedAccount.UserId);
            StatusText = error ?? $"Switched to {SelectedAccount.Display}. Launch Roblox to play on it.";
            Reload();
        }

        private void Delete()
        {
            if (SelectedAccount is null)
                return;

            var result = Frontend.ShowMessageBox(
                $"Forget {SelectedAccount.Display}? Its saved cookie will be deleted.",
                MessageBoxImage.Question,
                MessageBoxButton.YesNo
            );

            if (result != MessageBoxResult.Yes)
                return;

            App.AccountVault.Delete(SelectedAccount.UserId);
            SelectedAccount = null;
            OnPropertyChanged(nameof(HasSelection));
            StatusText = "Account forgotten.";
            Reload();
        }
    }
}
