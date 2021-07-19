///////////////////////////////////////////////////////////////////////////////////
// Open 3D Model Viewer (open3mod) (v0.1)
// [AnimationInspectionView.cs]
// (c) 2012-2013, Open3Mod Contributors
//
// Licensed under the terms and conditions of the 3-clause BSD license. See
// the LICENSE file in the root folder of the repository for the details.
//
// HIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE 
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
// ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; 
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND 
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT 
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS 
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
///////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace open3mod
{
    public sealed partial class AnimationInspectionView : UserControl
    {
        private readonly Scene _scene;
        private Timer _timer;
        private double _duration;
        private bool _playing;
        private double _animPlaybackSpeed = 1.0;

        private const int TimerInterval = 30;
        private const double PlaybackSpeedAdjustFactor = 0.6666;

        private int _speedAdjust;
        private const int MaxSpeedAdjustLevels = 8;

        private readonly Image _imagePlay;
        private readonly Image _imageStop;

        public AnimationInspectionView(Scene scene, TabPage tabPageAnimations)
        {
            _scene = scene;          
            
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            Dock = DockStyle.Fill;
            InitializeComponent();

            tabPageAnimations.Controls.Add(this);

            listBoxAnimations.Items.Add("None (Bind Pose)");

            if (scene.Raw.Animations != null)
            {
                foreach (var anim in scene.Raw.Animations)
                {
                    var dur = anim.DurationInTicks;
                    if (anim.TicksPerSecond > 1e-10)
                    {
                        dur /= anim.TicksPerSecond;
                    }
                    else
                    {
                        dur /= SceneAnimator.DefaultTicksPerSecond;
                    }
                    listBoxAnimations.Items.Add(string.Format("{0} ({1}s)", anim.Name, dur.ToString("0.000")));
                }                
            }
            listBoxAnimations.SelectedIndex = 0;

            checkBoxLoop.Checked = _scene.SceneAnimator.Loop;
            _imagePlay = ImageFromResource.Get("open3mod.Images.PlayAnim.png");
            _imageStop = ImageFromResource.Get("open3mod.Images.StopAnim.png");
            buttonPlay.Image = _imagePlay;

            // initially, animations are disabled.
            _scene.SceneAnimator.AnimationPlaybackSpeed = 0.0;
            labelSpeedValue.Text = "1.0x";

            timeSlideControl.Rewind += (o, args) =>
            {
                if (_scene.SceneAnimator.ActiveAnimation >= 0)
                {
                    _scene.SceneAnimator.AnimationCursor = args.NewPosition;
                }
            };
        }


        public bool Empty
        {
            get { return _scene.Raw.AnimationCount == 0; }
        }


        public bool Playing
        {
            get { return _playing; }
            set
            {
                if (value == _playing)
                {
                    return;
                }
                _playing = value;
                if (value)
                {
                    StartPlayingTimer();
                    _scene.SceneAnimator.AnimationPlaybackSpeed = _scene.SceneAnimator.ActiveAnimation >= 0 ? AnimPlaybackSpeed : 0.0;
                }
                else
                {
                    StopPlayingTimer();
                    _scene.SceneAnimator.AnimationPlaybackSpeed = 0.0;
                }
            }
        }


        public double AnimPlaybackSpeed
        {
            get { return _animPlaybackSpeed; }
            private set { 
                Debug.Assert(value > 1e-6, "use Playing=false to not play animations");
                _animPlaybackSpeed = value;

                // avoid float noise close to 1
                if (Math.Abs(_animPlaybackSpeed-1) < 1e-7)
                {
                    _animPlaybackSpeed = 1.0;
                }

                if (_playing)
                {
                    _scene.SceneAnimator.AnimationPlaybackSpeed = AnimPlaybackSpeed;
                }

                BeginInvoke(new MethodInvoker(() =>
                {
                    labelSpeedValue.Text = _animPlaybackSpeed.ToString("0.00") + "x";
                }));
            }
        }


        private void StopPlayingTimer()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer = null;
            }
        }


        private void StartPlayingTimer()
        {
            if (!Playing)
            {
                return;
            }

            _timer = new Timer { Interval = (TimerInterval) };
            _timer.Tick += (o, args) =>
            {
                var d = _scene.SceneAnimator.AnimationCursor;
                if (!_scene.SceneAnimator.IsInEndPosition)
                {
                    d %= _duration;
                }
                timeSlideControl.Position = d;
            };
            _timer.Start();
        }


        private void OnChangeSelectedAnimation(object sender, EventArgs e)
        {
            _scene.SceneAnimator.ActiveAnimation = listBoxAnimations.SelectedIndex - 1;
            if (_scene.SceneAnimator.ActiveAnimation >= 0)
            {
                var anim = _scene.Raw.Animations[_scene.SceneAnimator.ActiveAnimation];
                foreach (var control in panelAnimTools.Controls)
                {
                    if (control == buttonSlower && _speedAdjust == -MaxSpeedAdjustLevels ||
                        control == buttonFaster && _speedAdjust ==  MaxSpeedAdjustLevels)
                    {
                        continue;
                    }
                    ((Control)control).Enabled = true;
                }

                _duration = _scene.SceneAnimator.AnimationDuration;

                timeSlideControl.RangeMin = 0.0;
                timeSlideControl.RangeMax = _duration;
                timeSlideControl.Position = 0.0;
                _scene.SceneAnimator.AnimationCursor = 0;   

                StartPlayingTimer();
            }
            else
            {
                foreach(var control in panelAnimTools.Controls)
                {
                    ((Control) control).Enabled = false;
                }

                StopPlayingTimer();
            }
        }


        private void OnPlay(object sender, EventArgs e)
        {
            Playing = !Playing;
            buttonPlay.Image = Playing ? _imageStop : _imagePlay;
        }


        private void OnSlower(object sender, EventArgs e)
        {
            Debug.Assert(_speedAdjust > -MaxSpeedAdjustLevels);
            if (--_speedAdjust == -MaxSpeedAdjustLevels)
            {
                buttonSlower.Enabled = false;
            }
            buttonFaster.Enabled = true;
            AnimPlaybackSpeed *= PlaybackSpeedAdjustFactor;            
        }


        private void OnFaster(object sender, EventArgs e)
        {
            Debug.Assert(_speedAdjust < MaxSpeedAdjustLevels);
            if (++_speedAdjust == MaxSpeedAdjustLevels)
            {
                buttonFaster.Enabled = false;
            }
            buttonSlower.Enabled = true;
            AnimPlaybackSpeed /= PlaybackSpeedAdjustFactor;
        }


        private void OnChangeLooping(object sender, EventArgs e)
        {
            _scene.SceneAnimator.Loop = checkBoxLoop.Checked;
        }


        private void OnGoTo(object sender, KeyEventArgs e)
        {
            labelGotoError.Text = "";
            if (e.KeyCode != Keys.Enter)
            {               
                return;
            }

            var text = textBoxGoto.Text;
            double pos;
            try
            {
                pos = Double.Parse(text);
                if (pos < 0 || pos > _duration)
                {
                    throw new FormatException();
                }
            }
            catch(FormatException)
            {
                labelGotoError.Text = "Not a valid time";
                return;
            }
            Debug.Assert(pos >= 0);
            _scene.SceneAnimator.AnimationCursor = pos;
        }
    }
}

/* vi: set shiftwidth=4 tabstop=4: */ 