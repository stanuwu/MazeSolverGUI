using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.VisualBasic.CompilerServices;
using Microsoft.Win32;
using Color = System.Drawing.Color;
using Image = System.Drawing.Image;

namespace MazeSolverGUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool[,] _image = new bool[1000,1000];
        private bool[,] _map = new bool[1000, 1000];
        private bool[,] _path = new bool[1000, 1000];
        private Vector2? _start;
        private Vector2? _end;
        private Vector2? _boundsStart;
        private Vector2? _boundsEnd;

        private long its = 0;
        private long props = 0;
        private long parts = 0;
        private long alive = 0;
        private long dead = 0;
        
        private SolveState _state = SolveState.ImageSelect;
        private SolveState State {
            get
            {
                return _state;
            }
            set
            {
                StateChange(value);
                _state = value;
            }
        }

        enum SolveState
        {
            ImageSelect,
            StartSelect,
            EndSelect,
            BoundsSelect1,
            BoundsSelect2,
            Confirmation,
            Solving,
            Solved,
            Failed
        }
        
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Reset()
        {
            _image = new bool[1000,1000];
            _map = new bool[1000, 1000];
            _path = new bool[1000, 1000];
            _start = null;
            _end = null;
            _boundsStart = null;
            _boundsEnd = null;

            its = 0;
            props = 0;
            parts = 0;
            alive = 0;
            dead = 0;
            
            UpdateImage();
            UpdateStats();
        }

        private void UpdateStats()
        {
            ItsLabel.Content = $"{its} iterations";
            PropsLabel.Content = $"{props} propogations";
            PartsLabel.Content = $"{parts} particles";
            AliveLabel.Content = $"{alive} alive";
            DeadLabel.Content = $"{dead} dead";
        }
        
        private bool ShouldDrawDot(int x, int y, Vector2? pos)
        {
            if (!pos.HasValue) return false;
            return Vector2.DistanceSquared(new Vector2(x, y), pos.Value) < 25;
        }

        private void StateChange(SolveState state)
        {
            StateLabel.Content = $"State: {state}";
            UpdateImage();
        }

        private void UpdateImage()
        {
            MazeImage.Source = MakeImagePreview();
        }
        
        private ImageSource MakeImagePreview()
        {
            var img = new Bitmap(1000, 1000);
            for (int x = 0; x < 1000; x++)
            for (int y = 0; y < 1000; y++)
            {
                // BG
                img.SetPixel(x, y, Color.Black);
                
                // Particles
                if(_map[x, y]) img.SetPixel(x, y, Color.Yellow);
                
                // Path
                if(_path[x, y]) img.SetPixel(x, y, Color.Red);
                
                // Walls
                if(_image[x, y]) img.SetPixel(x, y, Color.White);
                
                // Start
                if(ShouldDrawDot(x, y, _start)) img.SetPixel(x, y, Color.Green);
                
                // End
                if(ShouldDrawDot(x, y, _end)) img.SetPixel(x, y, Color.Blue);
                
                // Bounds 1
                if(ShouldDrawDot(x, y, _boundsStart) && !_boundsEnd.HasValue) img.SetPixel(x, y, Color.Magenta);
                
                // Bounds Square
                if (_boundsStart.HasValue && _boundsEnd.HasValue)
                {
                    if((x > _boundsStart.Value.X - 2 && x < _boundsStart.Value.X + 2) || (x > _boundsEnd.Value.X - 2 && x < _boundsEnd.Value.X + 2) || (y > _boundsStart.Value.Y - 2 && y < _boundsStart.Value.Y + 2) || (y > _boundsEnd.Value.Y - 2 && y < _boundsEnd.Value.Y + 2)) img.SetPixel(x, y, Color.Magenta);
                }
            }
            return Imaging.CreateBitmapSourceFromHBitmap(img.GetHbitmap(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        }

        private void LoadImage(Bitmap original)
        {
            var img = new Bitmap(original, 900, 900);
            for (var x = 0; x < 900; x++)
            for (var y = 0; y < 900; y++)
            {
                var c = img.GetPixel(x, y);
                var gs = (int)(c.R * 0.3 + c.G * 0.59 + c.B * 0.11);
                _image[x + 50, y + 50] = gs < 127;
            }
        }
        
        private void LoadButton_OnClick(object sender, RoutedEventArgs e)
        {
            Reset();
            var fileDialog = new OpenFileDialog
            {
                Multiselect = false,
                Title = "Select Image",
                Filter = "Image files (*.png;*.jpeg;*.jpg;*.gif;*.bmp)|*.png;*.jpeg;*.jpg;*.gif;*.bmp|All files (*.*)|*.*"
            };
            if (fileDialog.ShowDialog() != true) return;
            try
            {
                var img = new Bitmap(fileDialog.FileName);
                LoadImage(img);
                State = SolveState.StartSelect;
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private void MazeImage_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            int cx = (int) Math.Round(e.GetPosition(MazeImage).X);
            int cy = (int) Math.Round(e.GetPosition(MazeImage).Y);

            switch (State)
            {
                case SolveState.StartSelect:
                    _start = new Vector2(cx, cy);
                    State = SolveState.EndSelect;
                    break;
                case SolveState.EndSelect:
                    _end = new Vector2(cx, cy);
                    State = SolveState.BoundsSelect1;
                    break;
                case SolveState.BoundsSelect1:
                    _boundsStart = new Vector2(cx, cy);
                    State = SolveState.BoundsSelect2;
                    break;
                case SolveState.BoundsSelect2:
                    float x1 = Math.Min(cx, _boundsStart.Value.X);
                    float x2 = Math.Max(cx, _boundsStart.Value.X);
                    float y1 = Math.Min(cy, _boundsStart.Value.Y);
                    float y2 = Math.Max(cy, _boundsStart.Value.Y);

                    _boundsStart = new Vector2(x1, y1);
                    _boundsEnd = new Vector2(x2, y2);
                    
                    State = SolveState.Confirmation;
                    break;
            }
        }

        private async void StartButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (State == SolveState.Confirmation)
            {
                State = SolveState.Solving;
                State = (await SolveMaze()) ? SolveState.Solved : SolveState.Failed;
            }
        }
        
        private async void StartFastButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (State == SolveState.Confirmation)
            {
                State = SolveState.Solving;
                State = (await SolveMaze(true)) ? SolveState.Solved : SolveState.Failed;
            }
        }

        private async Task<bool> SolveMaze(bool fast = false)
        {
            long count = 0;
            List<Particle> particles = new List<Particle>();
            particles.Add(new Particle((int)_start.Value.X, (int)_start.Value.Y, null));
            while (particles.Count > 0)
            {
                its++;
                count++;
                List<Particle> newParts = new List<Particle>();
                foreach (var part in particles)
                {
                    props++;
                    Particle[] spread = Propogate(part);
                    if(spread[0] != null) newParts.Add(spread[0]);
                    if(spread[1] != null) newParts.Add(spread[1]);
                    if(spread[2] != null) newParts.Add(spread[2]);
                    if(spread[3] != null) newParts.Add(spread[3]);
                }
                particles.AddRange(newParts);
                particles.RemoveAll(p => IsDead(p));
                alive = particles.Count;
                dead = parts - particles.Count;
                foreach (var part in particles)
                {
                    if (part.x == _end.Value.X && part.y == _end.Value.Y)
                    {
                        drawSolution(part);
                        UpdateImage();
                        UpdateStats();
                        return true;
                    }
                }

                if (!fast)
                {
                    if (count % 10 == 0)
                    {
                        UpdateImage();
                        UpdateStats();
                        await Task.Delay(10);
                    }
                }
            }
            return false;
        }

        private void drawSolution(Particle particle)
        {
            if(particle.source == null) return;
            _path[particle.x, particle.y] = true;
            drawSolution(particle.source);
        }
        
        private bool GetOrOOB(int x, int y)
        {
            if (x < 0 || x > 1000 || y < 0 || y > 1000) return true;
            if (_image[x, y]) return true;
            if (x < _boundsStart.Value.X || x > _boundsEnd.Value.X || y < _boundsStart.Value.Y || y > _boundsEnd.Value.Y) return true;
            return _map[x, y];
        }
        
        private bool IsDead(Particle particle)
        {
            return GetOrOOB(particle.x + 1, particle.y) && GetOrOOB(particle.x, particle.y + 1) && GetOrOOB(particle.x - 1, particle.y) && GetOrOOB(particle.x, particle.y - 1);
        }

        private Particle[] Propogate(Particle particle)
        {
            return
                new[]
                {
                    TryTakeField(particle.x + 1, particle.y, particle),
                    TryTakeField(particle.x, particle.y + 1, particle),
                    TryTakeField(particle.x - 1, particle.y, particle),
                    TryTakeField(particle.x, particle.y - 1, particle),
                };
        }

        private Particle TryTakeField(int x, int y, Particle particle)
        {
            if (!GetOrOOB(x, y))
            {
                parts++;
                _map[x, y] = true;
                return new Particle(x, y, particle);
            }
            return null;
        }
        
        private class Particle
        {
            public readonly int x;
            public readonly int y;
            public readonly Particle source;

            public Particle(int x, int y, Particle source)
            {
                this.x = x;
                this.y = y;
                this.source = source;
            }
        }
    }
}