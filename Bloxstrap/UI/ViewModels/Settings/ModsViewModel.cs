using System.Windows;
using System.Windows.Input;

using Microsoft.Win32;

using Windows.Win32;
using Windows.Win32.UI.Shell;
using Windows.Win32.Foundation;

using CommunityToolkit.Mvvm.Input;

using Bloxstrap.Models.SettingTasks;
using Bloxstrap.AppData;

namespace Bloxstrap.UI.ViewModels.Settings
{
    public class ModsViewModel : NotifyPropertyChangedViewModel
    {
        private void OpenModsFolder() => Process.Start("explorer.exe", Paths.Modifications);

        private readonly Dictionary<string, byte[]> FontHeaders = new()
        {
            { "ttf", new byte[4] { 0x00, 0x01, 0x00, 0x00 } },
            { "otf", new byte[4] { 0x4F, 0x54, 0x54, 0x4F } },
            { "ttc", new byte[4] { 0x74, 0x74, 0x63, 0x66 } } 
        };

        private void ManageCustomFont()
        {
            if (!String.IsNullOrEmpty(TextFontTask.NewState))
            {
                TextFontTask.NewState = "";
            }
            else
            {
                var dialog = new OpenFileDialog
                {
                    Filter = $"{Strings.Menu_FontFiles}|*.ttf;*.otf;*.ttc"
                };

                if (dialog.ShowDialog() != true)
                    return;

                string type = dialog.FileName.Substring(dialog.FileName.Length-3, 3).ToLowerInvariant();

                if (!FontHeaders.ContainsKey(type) 
                    || !FontHeaders.Any(x => File.ReadAllBytes(dialog.FileName).Take(4).SequenceEqual(x.Value)))
                {
                    Frontend.ShowMessageBox(Strings.Menu_Mods_Misc_CustomFont_Invalid, MessageBoxImage.Error);
                    return;
                }

                TextFontTask.NewState = dialog.FileName;
            }

            OnPropertyChanged(nameof(ChooseCustomFontVisibility));
            OnPropertyChanged(nameof(DeleteCustomFontVisibility));
        }

        public ICommand OpenModsFolderCommand => new RelayCommand(OpenModsFolder);

        public Visibility ChooseCustomFontVisibility => !String.IsNullOrEmpty(TextFontTask.NewState) ? Visibility.Collapsed : Visibility.Visible;

        public Visibility DeleteCustomFontVisibility => !String.IsNullOrEmpty(TextFontTask.NewState) ? Visibility.Visible : Visibility.Collapsed;

        public ICommand ManageCustomFontCommand => new RelayCommand(ManageCustomFont);

        public ICommand OpenCompatSettingsCommand => new RelayCommand(OpenCompatSettings);

        public ModPresetTask OldAvatarBackgroundTask { get; } = new("OldAvatarBackground", @"ExtraContent\places\Mobile.rbxl", "OldAvatarBackground.rbxl");

        public ModPresetTask OldCharacterSoundsTask { get; } = new("OldCharacterSounds", new()
        {
            { @"content\sounds\action_footsteps_plastic.mp3", "Sounds.OldWalk.mp3"  },
            { @"content\sounds\action_jump.mp3",              "Sounds.OldJump.mp3"  },
            { @"content\sounds\action_get_up.mp3",            "Sounds.OldGetUp.mp3" },
            { @"content\sounds\action_falling.mp3",           "Sounds.Empty.mp3"    },
            { @"content\sounds\action_jump_land.mp3",         "Sounds.Empty.mp3"    },
            { @"content\sounds\action_swim.mp3",              "Sounds.Empty.mp3"    },
            { @"content\sounds\impact_water.mp3",             "Sounds.Empty.mp3"    }
        });

        public EmojiModPresetTask EmojiFontTask { get; } = new();

        public EnumModPresetTask<Enums.CursorType> CursorTypeTask { get; } = new("CursorType", new()
        {
            {
                Enums.CursorType.From2006, new()
                {
                    { @"content\textures\Cursors\KeyboardMouse\ArrowCursor.png",    "Cursor.From2006.ArrowCursor.png"    },
                    { @"content\textures\Cursors\KeyboardMouse\ArrowFarCursor.png", "Cursor.From2006.ArrowFarCursor.png" }
                }
            },
            {
                Enums.CursorType.From2013, new()
                {
                    { @"content\textures\Cursors\KeyboardMouse\ArrowCursor.png",    "Cursor.From2013.ArrowCursor.png"    },
                    { @"content\textures\Cursors\KeyboardMouse\ArrowFarCursor.png", "Cursor.From2013.ArrowFarCursor.png" }
                }
            }
        });

        public FontModPresetTask TextFontTask { get; } = new();

        public DeathSoundModPresetTask DeathSoundTask { get; } = new();

        public CursorFileModPresetTask CustomCursorTask { get; } = new();

        private static readonly byte[] PngHeader = new byte[8] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        private static bool IsValidAudio(string path)
        {
            byte[] header = File.ReadAllBytes(path).Take(4).ToArray();
            if (header.Length < 4)
                return false;

            // ID3 tag, MP3 frame sync, or Ogg container
            return (header[0] == 0x49 && header[1] == 0x44 && header[2] == 0x33) ||
                   (header[0] == 0xFF && (header[1] & 0xE0) == 0xE0) ||
                   (header[0] == 0x4F && header[1] == 0x67 && header[2] == 0x67 && header[3] == 0x53);
        }

        private void ManageCustomDeathSound()
        {
            if (!String.IsNullOrEmpty(DeathSoundTask.NewState))
            {
                DeathSoundTask.NewState = "";
            }
            else
            {
                var dialog = new OpenFileDialog
                {
                    Filter = $"Audio files|*.mp3;*.ogg"
                };

                if (dialog.ShowDialog() != true)
                    return;

                if (!IsValidAudio(dialog.FileName))
                {
                    Frontend.ShowMessageBox("That file is not a valid MP3 or OGG audio file.", MessageBoxImage.Error);
                    return;
                }

                DeathSoundTask.NewState = dialog.FileName;
            }

            OnPropertyChanged(nameof(ChooseCustomDeathSoundVisibility));
            OnPropertyChanged(nameof(DeleteCustomDeathSoundVisibility));
        }

        private void ManageCustomCursor()
        {
            if (!String.IsNullOrEmpty(CustomCursorTask.NewState))
            {
                CustomCursorTask.NewState = "";
            }
            else
            {
                var dialog = new OpenFileDialog
                {
                    Filter = $"Image files|*.png"
                };

                if (dialog.ShowDialog() != true)
                    return;

                if (!File.ReadAllBytes(dialog.FileName).Take(8).SequenceEqual(PngHeader))
                {
                    Frontend.ShowMessageBox("That file is not a valid PNG image.", MessageBoxImage.Error);
                    return;
                }

                CustomCursorTask.NewState = dialog.FileName;

                // custom file and cursor packs share target files - packs lose
                CursorTypeTask.NewState = Enums.CursorType.Default;
            }

            OnPropertyChanged(nameof(ChooseCustomCursorVisibility));
            OnPropertyChanged(nameof(DeleteCustomCursorVisibility));
        }

        public Visibility ChooseCustomDeathSoundVisibility => !String.IsNullOrEmpty(DeathSoundTask.NewState) ? Visibility.Collapsed : Visibility.Visible;

        public Visibility DeleteCustomDeathSoundVisibility => !String.IsNullOrEmpty(DeathSoundTask.NewState) ? Visibility.Visible : Visibility.Collapsed;

        public ICommand ManageCustomDeathSoundCommand => new RelayCommand(ManageCustomDeathSound);

        public Visibility ChooseCustomCursorVisibility => !String.IsNullOrEmpty(CustomCursorTask.NewState) ? Visibility.Collapsed : Visibility.Visible;

        public Visibility DeleteCustomCursorVisibility => !String.IsNullOrEmpty(CustomCursorTask.NewState) ? Visibility.Visible : Visibility.Collapsed;

        public ICommand ManageCustomCursorCommand => new RelayCommand(ManageCustomCursor);

        private void OpenCompatSettings()
        {
            string path = new RobloxPlayerData().ExecutablePath;

            if (File.Exists(path))
                PInvoke.SHObjectProperties(HWND.Null, SHOP_TYPE.SHOP_FILEPATH, path, "Compatibility");
            else
                Frontend.ShowMessageBox(Strings.Common_RobloxNotInstalled, MessageBoxImage.Error);

        }
    }
}
