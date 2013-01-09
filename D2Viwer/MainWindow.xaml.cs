using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using System.Threading;
using System.IO;
using libZPlay;

namespace D2Viwer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region vars
        // Service objects
        #region service_vars
        Timer timer; // To fade "current track" caption
        int timer_interval = 1;
        double caption_fade = 0.005; // Opacity down for each timer tick
        ZPlay player = new ZPlay(); // To play tracks

        // Commands to actions associated with hotkeys
        static RoutedCommand back_command = new RoutedCommand();
        static RoutedCommand upscroll_command = new RoutedCommand();
        static RoutedCommand downscroll_command = new RoutedCommand();
        static RoutedCommand fullscreen_command = new RoutedCommand();

        // Default icons objects
        BitmapImage folder_icon = new BitmapImage(new Uri(AppDomain.CurrentDomain.BaseDirectory + "folder_icon.png"));
        BitmapImage disk_icon = new BitmapImage(new Uri(AppDomain.CurrentDomain.BaseDirectory + "disk_icon.png"));
        BitmapImage unknown_icon = new BitmapImage(new Uri(AppDomain.CurrentDomain.BaseDirectory + "unknown_icon.png"));

        // Service vars
        
        // Supported formats (may be expanded to all WPF.Image and ZPlay supported formats)
        string[] image_extensions = new[] { ".jpg", ".tiff", ".bmp", ".png", ".jpeg" }; 
        string[] audio_extensions = new[] { ".mp3", ".wav", ".ogg", ".flac", ".ape" };

        string start_directory = "C:\\";

        #endregion service_vars

        // Condition objects
        #region condition_vars
        bool fullscreen = false;
        DirectoryInfo current_directory;
        BitmapImage current_directory_image;
        List<DirectoryInfo> subdirs = new List<DirectoryInfo>();
        List<BitmapImage> subdirs_images = new List<BitmapImage>();
        List<string> tracks = new List<string>();
        int backleft, left, center, right, backright; // Subdirs icons identificators

        #endregion condition_vars

        #endregion vars

        #region methods
        #region init
        public MainWindow()
        {
            InitializeComponent();
            InitHotkeys();
            timer = new Timer(CallbackForTimer, null, 0, timer_interval);
            
            Load(new DirectoryInfo(start_directory));
        }

        /// <summary>
        /// Initialize hotkeys and mouse actions
        /// </summary>
        private void InitHotkeys()
        {
            KeyGesture key_gesture = null;
            MouseGesture mouse_gesture = null;
            InputBinding bind = null;

            CommandBindings.Add(new CommandBinding(back_command, GoBack));
            key_gesture = new KeyGesture(Key.Escape, ModifierKeys.None);
            mouse_gesture = new MouseGesture(MouseAction.RightClick);

            bind = new KeyBinding(back_command, key_gesture);
            InputBindings.Add(bind);

            bind = new MouseBinding(back_command, mouse_gesture);
            InputBindings.Add(bind);

            CommandBindings.Add(new CommandBinding(upscroll_command, RightScroll));
            key_gesture = new KeyGesture(Key.Left);
            bind = new KeyBinding(upscroll_command, key_gesture);
            InputBindings.Add(bind);

            CommandBindings.Add(new CommandBinding(downscroll_command, LeftScroll));
            key_gesture = new KeyGesture(Key.Right);
            bind = new KeyBinding(downscroll_command, key_gesture);
            InputBindings.Add(bind);

            CommandBindings.Add(new CommandBinding(fullscreen_command, SetFullscreen));
            key_gesture = new KeyGesture(Key.F12);
            bind = new KeyBinding(fullscreen_command, key_gesture);
            InputBindings.Add(bind);
        }

        /// <summary>
        /// Method for timer action
        /// </summary>
        /// <param name="param"></param>
        private void CallbackForTimer(object param)
        {
            Dispatcher.BeginInvoke((Action<object>)TimerTick, param);
        }

        #endregion init

        #region features
        /// <summary>
        /// Indicates, if current_directory is root directory for disk
        /// </summary>
        /// <returns></returns>
        private bool IsRoot()
        {
            return current_directory.Name == current_directory.FullName;
        }

        /// <summary>
        /// Each timer tick fade of track caption
        /// </summary>
        /// <param name="obj"></param>
        void TimerTick(object obj)
        {
            if (tbCurrentTrack.Opacity > 0.0)
            {
                tbCurrentTrack.Opacity -= caption_fade;
            }
        }

        /// <summary>
        /// Load date for specific directory
        /// </summary>
        /// <param name="directory"></param>
        private void Load(DirectoryInfo directory)
        {
            if (directory != null)
            {
                GetFiles(directory);
                LoadInterface();
            }
            else
            {
                // This is root
                LoadRoot();
            }
        }

        /// <summary>
        /// Load for list of disks 
        /// </summary>
        private void LoadRoot()
        {
            current_directory = null;
            current_directory_image = null;

            subdirs.Clear();
            subdirs_images.Clear();
            foreach (var disk in DriveInfo.GetDrives())
            {
                subdirs.Add(disk.RootDirectory);
                subdirs_images.Add(GetImage(disk.RootDirectory));
            }

            tracks.Clear();

            LoadInterface();
        }

        /// <summary>
        /// Get subdirs and tracks
        /// </summary>
        /// <param name="directory"></param>
        private void GetFiles(DirectoryInfo directory)
        {
            // Get dirs
            current_directory = directory;
            current_directory_image = GetImage(directory);

            subdirs.Clear();
            subdirs_images.Clear();
            foreach (var dir in current_directory.GetDirectories())
            {
                subdirs.Add(dir);
                subdirs_images.Add(GetImage(dir));
            }

            // Get audio files
            tracks.Clear();
            foreach (var track in
                current_directory.GetFiles()
                     .Where(f => audio_extensions.Contains(f.Extension.ToLower()))
                     .ToArray())
            {
                tracks.Add(track.Name);
            }
            if (tracks.Count > 0)
            {
                PlayTrack(tracks[0]);
            }
        }

        /// <summary>
        /// Get first image for directory
        /// </summary>
        /// <param name="directory"></param>
        /// <returns></returns>
        private BitmapImage GetImage(DirectoryInfo directory)
        {
            try
            {
                // Find images, select first
                FileInfo[] image_files = directory.GetFiles()
                         .Where(f => image_extensions.Contains(f.Extension.ToLower()))
                         .ToArray();
                if (image_files.Length > 0)
                {
                    string path_to_image = image_files[0].FullName;
                    BitmapImage image = new BitmapImage(new Uri(path_to_image));
                    return image;
                }
                else
                {
                    // or get standard disk/folder icon
                    // Disk don't have parent
                    if (directory.FullName == directory.Name)
                    {
                        return disk_icon;
                    }
                    else
                    {
                        return folder_icon;
                    }
                }
            }
            catch
            {
                return unknown_icon;
            }
        }

        /// <summary>
        /// Play specific track
        /// </summary>
        /// <param name="name"></param>
        private void PlayTrack(string name)
        {
            player.OpenFile(current_directory.FullName + "\\" + name, TStreamFormat.sfUnknown);
            player.StartPlayback();

            // Make faded caption
            tbCurrentTrack.Text = "Now playing: " + name;
            tbCurrentTrack.Opacity = 1.0;
        }

        #endregion features

        #region interaction
        /// <summary>
        /// Switch on/off fullscreen mode (without disable taskbar)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SetFullscreen(object sender, RoutedEventArgs e)
        {
            if (fullscreen == false)
            {
                this.WindowStyle = WindowStyle.None;
                this.WindowState = WindowState.Maximized;
                this.Topmost = true;
                this.fullscreen = true;
            }
            else
            {
                this.WindowState = WindowState.Maximized;
                this.WindowStyle = WindowStyle.ThreeDBorderWindow;
                this.Topmost = false;
                this.fullscreen = false;
            }
        }

        /// <summary>
        /// Scroll left for one item
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LeftScroll(object sender, RoutedEventArgs e)
        {
            if (right == -1)
            {
            }
            else
            {
                int last;
                if (backright == -1)
                {
                    last = -1;
                }
                else
                    if (backright + 1 < subdirs.Count)
                    {
                        last = backright + 1;
                    }
                    else
                    {
                        last = -1;
                    }
                LoadSubdirs(left, center, right, backright, last);
            }
        }

        /// <summary>
        /// Scroll right to one item
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RightScroll(object sender, RoutedEventArgs e)
        {
            if (left == -1)
            {
            }
            else
            {
                int last;
                if (backleft > 0)
                {
                    last = backleft - 1;
                }
                else
                {
                    last = -1;
                }
                LoadSubdirs(last, backleft, left, center, right);
            }
        }

        /// <summary>
        /// Go to:
        /// - skip (if current_directory already rool)
        /// - root, list of disks (if current_directory is disk)
        /// - parent folder (if current_directory is normal directory)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GoBack(object sender, RoutedEventArgs e)
        {
            if (current_directory != null)
            {

                if (IsRoot())
                {
                    Load(null);
                }
                else
                {
                    Load(current_directory.Parent);
                }
            }
        }

        /// <summary>
        /// Load interface for loaded data
        /// </summary>
        private void LoadInterface()
        {
            // Root directory
            if (current_directory != null)
            {
                this.Title = current_directory.Name;
                tbCurrentFolder.Text = current_directory.Name;
                imgCurrentFolder.Source = current_directory_image;
            }
            else
            {
                this.Title = "D2Viwer";
                tbCurrentFolder.Text = "";
                imgCurrentFolder.Source = null;
            }

            // Subdirectories
            LoadSubdirs();

            // Tracks
            lboxTracks.Items.Clear();
            foreach (var track in tracks)
            {
                lboxTracks.Items.Add(track);
            }
        }

        /// <summary>
        /// Load subdirs for loaded data with collocation (center element must exist, etc...)
        /// </summary>
        private void LoadSubdirs()
        {
            if (subdirs.Count >= 5)
            {
                // Show first 5
                LoadSubdirs(0, 1, 2, 3, 4);
            }
            else
            {
                if (subdirs.Count >= 3)
                {
                    int[] indexes = new int[] { 0, 1, 2, -1, -1 };
                    if (subdirs.Count == 4)
                    {
                        indexes[3] = 3;
                    }
                    LoadSubdirs(indexes[0], indexes[1], indexes[2], indexes[3], indexes[4]);
                }
                else
                {
                    if (subdirs.Count == 2)
                    {
                        LoadSubdirs(-1, 0, 1, -1, -1);
                    }
                    else if (subdirs.Count == 1)
                    {
                        LoadSubdirs(-1, -1, 0, -1, -1);
                    }
                    else
                    {
                        LoadSubdirs(-1, -1, -1, -1, -1);
                    }
                }
            }
        }

        /// <summary>
        /// Load subdirs icons and captions (>=0 - subdirs index, -1 - none)
        /// </summary>
        /// <param name="backleft"></param>
        /// <param name="left"></param>
        /// <param name="center"></param>
        /// <param name="right"></param>
        /// <param name="backright"></param>
        private void LoadSubdirs(int backleft, int left, int center, int right, int backright)
        {
            this.backleft = backleft;
            this.left = left;
            this.center = center;
            this.right = right;
            this.backright = backright;

            if (backleft != -1)
            {
                tbBackLeft.Text = subdirs[backleft].Name;
                imgBackLeft.Source = subdirs_images[backleft];
                imgBackLeft.Tag = subdirs[backleft]; // Tag contains folder info
            }
            else
            {
                tbBackLeft.Text = "";
                imgBackLeft.Source = null;
                imgBackLeft.Tag = null;
            }

            if (left != -1)
            {
                tbLeft.Text = subdirs[left].Name;
                imgLeft.Source = subdirs_images[left];
                imgLeft.Tag = subdirs[left];
            }
            else
            {
                tbLeft.Text = "";
                imgLeft.Source = null;
                imgLeft.Tag = null;
            }

            if (center != -1)
            {
                tbCenter.Text = subdirs[center].Name;
                imgCenter.Source = subdirs_images[center];
                imgCenter.Tag = subdirs[center];
            }
            else
            {
                tbCenter.Text = "";
                imgCenter.Source = null;
                imgCenter.Tag = null;
            }

            if (right != -1)
            {
                tbRight.Text = subdirs[right].Name;
                imgRight.Source = subdirs_images[right];
                imgRight.Tag = subdirs[right];
            }
            else
            {
                tbRight.Text = "";
                imgRight.Source = null;
                imgRight.Tag = null;
            }

            if (backright != -1)
            {
                tbBackRight.Text = subdirs[backright].Name;
                imgBackRight.Source = subdirs_images[backright];
                imgBackRight.Tag = subdirs[backright];
            }
            else
            {
                tbBackRight.Text = "";
                imgBackRight.Source = null;
                imgBackRight.Tag = null;
            }
        }

        /// <summary>
        /// Click for directory icon
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void imgFolder_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                DirectoryInfo dir = (DirectoryInfo)((Image)sender).Tag;
                Load(dir);
            }
            catch
            {
            }
        }

        /// <summary>
        /// For change tracks
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void lboxTracks_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            PlayTrack(lboxTracks.SelectedItem.ToString());
        }

        /// <summary>
        /// For mouse scroll
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0)
            {
                LeftScroll(null, null);
            }
            else
            {
                RightScroll(null, null);
            }
        }

        #endregion interaction
        #endregion methods
    }
}
