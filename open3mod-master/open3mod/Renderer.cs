///////////////////////////////////////////////////////////////////////////////////
// Open 3D Model Viewer (open3mod) (v0.1)
// [Renderer.cs]
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
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using System.Diagnostics;

namespace open3mod
{
    public class Renderer : IDisposable
    {
        private readonly MainWindow _window;
        private readonly TextOverlay _textOverlay;

        private Image[,] _hudImages;
        private bool _hudDirty = true;

        private double _accTime;
        private readonly float[] _lastActiveVp = new float[4];
        private Point _mousePos;
        private Rectangle _hoverRegion;
        private double _displayFps;
        private Vector4 _hoverViewport;
        private Point _mouseClickPos;
        private bool _processHudClick;
        private Tab.ViewIndex _hoverViewIndex;
        private float _hoverFadeInTime;

        private Matrix4 _lightRotation = Matrix4.Identity;

        public delegate void GlExtraDrawJobDelegate(object sender);

        /// <summary>
        /// This event is fired every draw frame to allow other editor
        /// components to access the Gl subsystem in a safe manner.
        /// 
        /// This event is always invoced on the single thread that is responsible
        /// for interacting with Gl.
        /// </summary>
        public event GlExtraDrawJobDelegate GlExtraDrawJob;
        private void OnGlExtraDrawJob()
        {
            var handler = GlExtraDrawJob;
            if (handler != null)
            {
                handler(this);
                // reset all event handlers - extra draw job get executed only once
                // TODO: what if handlers re-register themselves?
                GlExtraDrawJob = null;
            }
        }


        // Some colors and other tweakables
        protected Color HudColor
        {
            get { return Color.FromArgb(100, 80, 80, 80); }
        }


        protected Color BorderColor
        {
            get { return Color.DimGray; }
        }


        protected Color ActiveBorderColor
        {
            get { return Color.GreenYellow; }
        }


        protected Color BackgroundColor
        {
            get { return Color.FromArgb(255, 165, 166, 165); }
        }


        protected Color ActiveViewColor
        {
            get { return Color.FromArgb(255, 175, 175, 175); }
        }


        protected float HudHoverTime
        {
            get { return 0.2f; }
        }



        /// <summary>
        /// The gl context which is being rendered to
        /// </summary>
        public GLControl GlControl
        {
            get { return _window.GlControl; }
        }

        /// <summary>
        /// Host window
        /// </summary>
        public MainWindow Window
        {
            get { return _window; }
        }


        /// <summary>
        /// Utility object in charge of maintaining all text overlays
        /// </summary>
        public TextOverlay TextOverlay
        {
            get { return _textOverlay; }
        }

        /// <summary>
        /// Obtain actual rendering resolution in pixels
        /// </summary>
        public Size RenderResolution
        {
            get { return GlControl.ClientSize; }
        }

        public Matrix4 LightRotation
        {
            get { return _lightRotation; }
        }


        /// <summary>
        /// Construct a renderer given a valid and fully loaded MainWindow
        /// </summary>
        /// <param name="window">Main window, Load event of the GlContext
        ///    needs to be fired already.</param>
        internal Renderer(MainWindow window)
        {
            _window = window;
            _textOverlay = new TextOverlay(this);
        }


      

        /// <summary>
        /// Perform any non-drawing operations that need to be executed
        /// once per frame and whose implementation resides in Renderer.
        /// </summary>
        public void Update(double delta)
        {
            if (_hoverFadeInTime > 0)
            {
                _hoverFadeInTime -= (float) delta;
                _hudDirty = true;
            }
        }


        /// <summary>
        /// Draw the contents of a given tab. If the tab contains a scene,
        /// this scene is drawn. If the tab is in loading or failed state,
        /// the corresponding info screen will be drawn.
        /// </summary>
        /// <param name="activeTab">Tab containing the scene to be drawn</param>
        public void Draw(Tab activeTab)
        {
            GL.DepthMask(true);
            GL.ClearColor(BackgroundColor);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            var ui = Window.UiState.ActiveTab;

            // draw viewport 3D contents
            if (ui.ActiveScene != null)
            {
                var index = Tab.ViewIndex.Index0;
                foreach (var viewport in ui.ActiveViews)
                {   
                    // always draw the active viewport last 
                    if (viewport == null || ui.ActiveViewIndex == index)
                    {
                        ++index;
                        continue;
                    }
                    var view = viewport.Bounds;
                    var cam = viewport.ActiveCameraControllerForView();
                    DrawViewport(cam, activeTab, view.X, view.Y, view.Z, view.W, ui.ActiveViewIndex == index);
                    ++index;
                }

                var activeVp = ui.ActiveViews[(int)ui.ActiveViewIndex];
                Debug.Assert(activeVp != null);

                var activeVpBounds = activeVp.Bounds;
                DrawViewport(activeVp.ActiveCameraControllerForView(),
                    activeTab, 
                    activeVpBounds.X, 
                    activeVpBounds.Y, 
                    activeVpBounds.Z, 
                    activeVpBounds.W, true);

                if (ui.ActiveViewMode != Tab.ViewMode.Single)
                {
                    SetFullViewport();
                }

                if (Window.UiState.ShowFps)
                {
                    DrawFps();
                }

                if (!_window.IsDraggingViewportSeparator)
                {
                    if (!_hudHidden)
                    {
                        DrawHud();
                    }
                }
                else
                {
                    _textOverlay.WantRedrawNextFrame = true;
                    _hudHidden = true;
                }
            }
            else
            {
                SetFullViewport();
                if (activeTab.State == Tab.TabState.Failed)
                {
                    DrawFailureSplash(activeTab.ErrorMessage);
                }
                else if (activeTab.State == Tab.TabState.Loading)
                {
                    DrawLoadingSplash();
                }
                else
                {
                    Debug.Assert(activeTab.State == Tab.TabState.Empty);
                    DrawNoSceneSplash();
                }
            }
            _textOverlay.Draw();

            // draw viewport finishing (i.e. contours)
            if (ui.ActiveScene != null && ui.ActiveViewMode != Tab.ViewMode.Single)
            {
                var index = Tab.ViewIndex.Index0;
                foreach (var viewport in ui.ActiveViews)
                {
                    // always draw the active viewport last 
                    if (viewport == null || ui.ActiveViewIndex == index)
                    {
                        ++index;
                        continue;
                    }

                    var view = viewport.Bounds;
                    DrawViewportPost(view.X, view.Y, view.Z, view.W, false);
                    ++index;
                }

                var activeVp = ui.ActiveViews[(int) ui.ActiveViewIndex];
                Debug.Assert(activeVp != null);

                var activeVpBounds = activeVp.Bounds;
                DrawViewportPost(activeVpBounds.X, activeVpBounds.Y,activeVpBounds.Z, activeVpBounds.W, true);
            }

            // handle other Gl jobs such as drawing preview images - components
            // use this event to register their jobs.
            OnGlExtraDrawJob();
        }


        /// <summary>
        /// Draw a string with four shifted copies of itself (shadow)
        /// </summary>
        /// <param name="graphics">graphics context</param>
        /// <param name="s">string to be drawn</param>
        /// <param name="font"></param>
        /// <param name="rect">graphics.DrawString() draw rectangle</param>
        /// <param name="main">text color</param>
        /// <param name="shadow">color of the text's shadow (make partially transparent at your leisure)</param>
        /// <param name="format">graphics.DrawString() formatting info</param>
        private void DrawShadowedString(Graphics graphics, String s, Font font, RectangleF rect, Color main, Color shadow,
            StringFormat format)
        {
            using (var sb = new SolidBrush(main))
            {
                using (var sb2 = new SolidBrush(shadow))
                {
                    for (int xd = -1; xd <= 1; xd += 2)
                    {
                        for (int yd = -1; yd <= 1; yd += 2)
                        {
                            var rect2 = new RectangleF(rect.Left + xd,
                               rect.Top + yd,
                               rect.Width,
                               rect.Height);

                            graphics.DrawString(s, font, sb2, rect2, format);
                        }
                    }
                    graphics.DrawString(s, font, sb, rect, format);
                }
            }
        }

        private bool _hoverViewWasActive = false;


        /// <summary>
        /// Draw HUD (camera panel) at the viewport that the mouse is currently hovering over
        /// </summary>
        private void DrawHud()
        {
            // sanity check whether the _hoverViewIndex is ok
            var ui = Window.UiState.ActiveTab;
            if(ui.ActiveViews[(int)_hoverViewIndex] == null)
            {
                return;
            }
      
            var x1 = _hoverViewport.X;
            var y1 = _hoverViewport.Y;
            var x2 = _hoverViewport.Z;
            var y2 = _hoverViewport.W;

            if (!_hudDirty)
            {
                // ReSharper disable CompareOfFloatsByEqualityOperator
                _hudDirty = x1 != _lastActiveVp[0] || y1 != _lastActiveVp[1] || x2 != _lastActiveVp[2] ||
                            y2 != _lastActiveVp[3];
                // ReSharper restore CompareOfFloatsByEqualityOperator
            }

            // hack to make sure the HUD is redrawn if the active camera mode changed without the
            // user hovering over the HUD area. This can happen when auto-changing from one of the
            // axis-lock-in modes to orbit mode.
            var newMode = ui.ActiveCameraControllerForView(_hoverViewIndex).GetCameraMode();
            if (newMode != _lastHoverViewCameraMode)
            {
                _hudDirty = true;
            }

            bool hoverViewIsActive = _hoverViewIndex == ui.ActiveViewIndex;
            if (hoverViewIsActive != _hoverViewWasActive)
            {
                _hudDirty = true;
            }
            _hoverViewWasActive = hoverViewIsActive;

            _lastHoverViewCameraMode = newMode;

            _lastActiveVp[0] = x1;
            _lastActiveVp[1] = y1;
            _lastActiveVp[2] = x2;
            _lastActiveVp[3] = y2;

            if (!_textOverlay.WantRedraw)
            {
                if (_hudDirty)
                {
                    _textOverlay.WantRedrawNextFrame = true;
                }            
                return;
            }

            _hudDirty = false;

            LoadHudImages();
            Debug.Assert(_hudImages != null);

            var graphics = _textOverlay.GetDrawableGraphicsContext();
            if (graphics == null)
            {
                return;
            }

            var xPoint = 3 + (int) (x2*(double) RenderResolution.Width);
            var yPoint = (int) ((1.0f - y2)*(double) RenderResolution.Height); // note: y is flipped

            //padding if bounds are drawn
            if (ui.ActiveViewMode != Tab.ViewMode.Single)
            {
                xPoint -= 3;
                yPoint += 3;
            }

            const int xSpacing = 4;

            var imageWidth = _hudImages[0, 0].Width;
            var imageHeight = _hudImages[0, 0].Height;

            var regionWidth = imageWidth*_hudImages.GetLength(0) + xSpacing*(_hudImages.GetLength(0) - 1);
            const int regionHeight = 27;

            if (regionWidth > (x2 - x1) * RenderResolution.Width || regionHeight > (y2 - y1) * RenderResolution.Height)
            {
                return;
            }

            xPoint -= regionWidth;
            _hoverRegion = new Rectangle(xPoint, yPoint, regionWidth - 2, regionHeight);

            if (_hoverFadeInTime > 0.0f)
            {
                var cm = new ColorMatrix();
                var ia = new ImageAttributes();
                cm.Matrix33 = 1.0f - _hoverFadeInTime/HudHoverTime;
                ia.SetColorMatrix(cm);

                graphics.DrawImage(_hudBar, _hoverRegion, 0, 0, _hudBar.Width, _hudBar.Height, GraphicsUnit.Pixel, ia);
            }
            else
            {
                graphics.DrawImage(_hudBar, _hoverRegion);
            }

            // draw reset info stringin the upper left corner
            if (_hoverViewIndex == ui.ActiveViewIndex)
            {
                var format = new StringFormat { LineAlignment = StringAlignment.Near, Alignment = StringAlignment.Near };
                var rect = new RectangleF(x1 * RenderResolution.Width + 10,
                    (1 - y2) * RenderResolution.Height + 10,
                    (x2 - x1) * RenderResolution.Width,
                    (y2 - y1) * RenderResolution.Height);

                DrawShadowedString(graphics, "Press [R] to reset the view", Window.UiState.DefaultFont10,
                    rect, Color.Black, Color.FromArgb(50, Color.White), format);         
            }

            // draw all the buttons on the HUD
            xPoint += _hudImages.GetLength(0) / 2;
            for (var i = 0; i < _hudImages.GetLength(0); ++i)
            {
                var x = xPoint;
                var y = yPoint + 4;
                var w = (int)(imageWidth * 2.0 / 3);
                var h = (int)(imageHeight * 2.0 / 3);

                if (_processHudClick &&
                    _mouseClickPos.X > x && _mouseClickPos.X <= x + w &&
                    _mouseClickPos.Y > y && _mouseClickPos.Y <= y + h)
                {
                    _processHudClick = false;

                    ui.ChangeCameraModeForView(_hoverViewIndex, (CameraMode)i);
                    //Debug.Assert(ui.ActiveCameraControllerForView(_hoverViewIndex).GetCameraMode() == (CameraMode)i);
                }

                // normal image
                var imageIndex = 0;
                bool inside = _mousePos.X > x && _mousePos.X <= x + w && _mousePos.Y > y && _mousePos.Y <= y + h;

                if (ui.ActiveCameraControllerForView(_hoverViewIndex).GetCameraMode() == (CameraMode)i)
                {
                    // selected image
                    imageIndex = 2;
                }
                else if (inside)
                {
                    // hover image
                    imageIndex = 1;
                }
                if (inside)
                {
                    // draw tooltip
                    Debug.Assert(i < DescTable.Length);

                    var format = new StringFormat { LineAlignment = StringAlignment.Near, Alignment = StringAlignment.Far };
                    var rect = new RectangleF(x1 * RenderResolution.Width,
                        (1 - y2) * RenderResolution.Height + 35,
                        (x2 - x1) * RenderResolution.Width - 10,
                        (y2 - y1) * RenderResolution.Height - 2);

                    DrawShadowedString(graphics, DescTable[i], Window.UiState.DefaultFont10,
                        rect, Color.Black, Color.FromArgb(50, Color.White), format);         
                }

                var img = _hudImages[i, imageIndex];

                //Debug.Assert(img.Width == imageWidth && img.Height == imageHeight, 
                //    "all images must be of the same size");

                /*
                var cm = new ColorMatrix();
                var ia = new ImageAttributes();
                cm.Matrix00 = 1.5f;
                cm.Matrix11 = 1.5f;
                cm.Matrix22 = 1.5f;
                ia.SetColorMatrix(cm);
                */
                graphics.DrawImage(img, new Rectangle(x, y, w, h), 0,0,img.Width,img.Height,GraphicsUnit.Pixel,null);
                xPoint += imageWidth;
            }
        }


        public void OnMouseMove(MouseEventArgs mouseEventArgs, Vector4 viewport, Tab.ViewIndex viewIndex)
        {
            _mousePos = mouseEventArgs.Location;
            if (_mousePos.X > _hoverRegion.Left && _mousePos.X <= _hoverRegion.Right &&
                _mousePos.Y > _hoverRegion.Top && _mousePos.Y <= _hoverRegion.Bottom)
            {
                _hudDirty = true;
            }
            if (viewport == _hoverViewport)
            {
                return;
            }
            _hudDirty = true;
            _hoverViewport = viewport;
            _hoverViewIndex = viewIndex;
            _hoverFadeInTime = HudHoverTime;
            _hudHidden = false;
        }


        public void OnMouseClick(MouseEventArgs mouseEventArgs, Vector4 viewport, Tab.ViewIndex viewIndex)
        {
            // a bit hacky - the click is processed by the render routine. But this is by
            // far the simplest way to get this done without duplicating code.
            if (_mousePos.X > _hoverRegion.Left && _mousePos.X <= _hoverRegion.Right &&
                _mousePos.Y > _hoverRegion.Top && _mousePos.Y <= _hoverRegion.Bottom)
            {
                _mouseClickPos = mouseEventArgs.Location;
                _processHudClick = true;
                _hudDirty = true;
            }
        }


        private static readonly string[] DescTable = new[] 
        { 
            "Lock on X axis", 
            "Lock on Y axis", 
            "Lock on Z axis", 
            "Orbit view", 
            "First-person view - use WASD or arrows to move",
      // Picking view is not implemented yet
      //      "Info picker"
        };


        private static readonly string[] PrefixTable = new[]
        {
            "open3mod.Images.HUD_X",
            "open3mod.Images.HUD_Y",
            "open3mod.Images.HUD_Z",
            "open3mod.Images.HUD_Orbit",
            "open3mod.Images.HUD_FPS",
      // Picking view is not implemented yet
      //      "open3mod.Images.HUD_Picking"
        };


        private static readonly string[] PostFixTable = new[]
        {
            "_Normal",
            "_Hover",
            "_Selected"
        };

        private bool _hudHidden;
        private CameraMode _lastHoverViewCameraMode;
        private Image _hudBar;


        /// <summary>
        /// Populate _hudImages
        /// </summary>
        private void LoadHudImages()
        {
            if (_hudImages != null)
            {
                return;
            }

            _hudImages = new Image[PrefixTable.Length, 3];              
            for (var i = 0; i < _hudImages.GetLength(0); ++i)
            {
                for (var j = 0; j < _hudImages.GetLength(1); ++j)
                {
                    _hudImages[i, j] = ImageFromResource.Get(PrefixTable[i] + PostFixTable[j] + ".png");
                }
            }

            _hudBar = ImageFromResource.Get("open3mod.Images.HUDBar.png");
        }


        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }


        protected virtual void Dispose(bool disposing)
        {
            _textOverlay.Dispose();
        }


        /// <summary>
        /// Respond to window changes. Users normally do not need to call this.
        /// </summary>
        public void Resize()
        {
            _textOverlay.Resize();
        }


        /// <summary>
        /// Draw a scene to a viewport using an ICameraController to specify the camera.
        /// </summary>
        /// <param name="view">Active cam controller for this viewport</param>
        /// <param name="activeTab">Scene to be drawn</param>
        /// <param name="xs">X-axis starting point of the viewport in range [0,1]</param>
        /// <param name="ys">Y-axis starting point of the viewport in range [0,1]</param>
        /// <param name="xe">X-axis end point of the viewport in range [0,1]</param>
        /// <param name="ye">X-axis end point of the viewport in range [0,1]</param>
        /// <param name="active"></param>
        private void DrawViewport(ICameraController view, Tab activeTab, double xs, double ys, double xe,
                                  double ye, bool active = false)
        {
            // update viewport 
            var w = (double) RenderResolution.Width;
            var h = (double) RenderResolution.Height;

            var vw = (int) ((xe - xs)*w);
            var vh = (int) ((ye - ys)*h);
            GL.Viewport((int) (xs*w), (int) (ys*h), (int) ((xe - xs)*w), (int) ((ye - ys)*h));

            DrawViewportColorsPre(active);
            var aspectRatio = (float)vw/vh;

            // set a proper perspective matrix for rendering
            Matrix4 perspective = Matrix4.CreatePerspectiveFieldOfView(MathHelper.PiOver4, aspectRatio, 0.001f, 100.0f);
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadMatrix(ref perspective);

            if (activeTab.ActiveScene != null)
            {
                DrawScene(activeTab.ActiveScene, view);
            }
        }


        private void DrawViewportPost(double xs, double ys, double xe,
                                  double ye, bool active = false)
        {
            // update viewport 
            var w = (double)RenderResolution.Width;
            var h = (double)RenderResolution.Height;

            var vw = (int)((xe - xs) * w);
            var vh = (int)((ye - ys) * h);
            GL.Viewport((int)(xs * w), (int)(ys * h), (int)((xe - xs) * w), (int)((ye - ys) * h));
            DrawViewportColorsPost(active, vw, vh);
        }



        private void SetFullViewport()
        {
            GL.Viewport(0, 0, RenderResolution.Width, RenderResolution.Height);
        }


        private void DrawViewportColorsPre(bool active)
        {
            if (!active)
            {
                return;
            }
            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadIdentity();
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();

            // paint the active viewport in a slightly different shade of gray,
            // overwriting the initial background color.
            GL.Color4(ActiveViewColor);
            GL.Rect(-1, -1, 1, 1);
        }


        private void DrawViewportColorsPost(bool active, int width, int height)
        {
            GL.Hint(HintTarget.LineSmoothHint, HintMode.Nicest);

            var texW = 1.0/width;
            var texH = 1.0/height;

            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadIdentity();
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();

            var lineWidth = active ? 4 : 3;

            // draw contour line
            GL.LineWidth(lineWidth);
            GL.Color4(active ? ActiveBorderColor : BorderColor);

            var xofs = lineWidth*0.5*texW;
            var yofs = lineWidth*0.5*texH;

            GL.Begin(BeginMode.LineStrip);
            GL.Vertex2(-1.0 + xofs, -1.0 + yofs);
            GL.Vertex2(1.0 - xofs, -1.0 + yofs);
            GL.Vertex2(1.0 - xofs, 1.0 - yofs);
            GL.Vertex2(-1.0 + xofs, 1.0 - yofs);
            GL.Vertex2(-1.0 + xofs, -1.0 + yofs);
            GL.End();

            GL.LineWidth(1);
            GL.MatrixMode(MatrixMode.Modelview);
        }


        private void DrawScene(Scene scene, ICameraController view)
        {
            Debug.Assert(scene != null);
            scene.Render(Window.UiState, view, this);
        }


        private void DrawNoSceneSplash()
        {
            var graphics = _textOverlay.GetDrawableGraphicsContext();
            if(graphics == null)
            {
                return;
            }

            var format = new StringFormat();
            format.LineAlignment = StringAlignment.Center;
            format.Alignment = StringAlignment.Center;

            graphics.DrawString("Drag file here", Window.UiState.DefaultFont16,
                                new SolidBrush(Color.Black),
                                new RectangleF(0, 0, GlControl.Width, GlControl.Height),
                                format);
        }


        private void DrawLoadingSplash()
        {
            var graphics = _textOverlay.GetDrawableGraphicsContext();
            if (graphics == null)
            {
                return;
            }

            var format = new StringFormat {LineAlignment = StringAlignment.Center, Alignment = StringAlignment.Center};

            graphics.DrawString("Loading ...", Window.UiState.DefaultFont16,
                                new SolidBrush(Color.Black),
                                new RectangleF(0, 0, GlControl.Width, GlControl.Height),
                                format);
        }


        private void DrawFailureSplash(string message)
        {
            var graphics = _textOverlay.GetDrawableGraphicsContext();
            if (graphics == null)
            {
                return;
            }

            var format = new StringFormat {LineAlignment = StringAlignment.Center, Alignment = StringAlignment.Center};

            // hack: re-use the image we use for failed texture imports :-)
            var img = TextureThumbnailControl.GetLoadErrorImage();

            graphics.DrawImage(img, GlControl.Width/2 - img.Width/2, GlControl.Height/2 - img.Height - 30, img.Width,
                               img.Height);
            graphics.DrawString("Sorry, this scene failed to load.", Window.UiState.DefaultFont16,
                                new SolidBrush(Color.Red),
                                new RectangleF(0, 0, GlControl.Width, GlControl.Height),
                                format);

            graphics.DrawString("What the importer said went wrong: " + message, Window.UiState.DefaultFont12,
                                new SolidBrush(Color.Black),
                                new RectangleF(0, 100, GlControl.Width, GlControl.Height),
                                format);
        }


        private void DrawFps()
        {
            // only update every 1/3rd of a second
            _accTime += Window.Fps.LastFrameDelta;
            if (_accTime < 0.3333 && !_textOverlay.WantRedraw)
            {
                if (_accTime >= 0.3333)
                {
                    _textOverlay.WantRedrawNextFrame = true;
                }
                return;
            }

            if (_accTime >= 0.3333)
            {
                _displayFps = Window.Fps.LastFps;
                _accTime = 0.0;
            }

            var graphics = _textOverlay.GetDrawableGraphicsContext();
            if (graphics == null)
            {
                return;
            }
            graphics.DrawString("FPS: " + _displayFps.ToString("0.0"), Window.UiState.DefaultFont12,
                                new SolidBrush(Color.Red), 5, 5);
        }


        public void HandleLightRotationOnMouseMove(int mouseDeltaX, int mouseDeltaY, ref Matrix4 view)
        {
            _lightRotation = _lightRotation * Matrix4.CreateFromAxisAngle(view.Column1.Xyz, mouseDeltaX * 0.005f);
            _lightRotation = _lightRotation * Matrix4.CreateFromAxisAngle(view.Column0.Xyz, mouseDeltaY * 0.005f);
        }
    }
}

/* vi: set shiftwidth=4 tabstop=4: */ 