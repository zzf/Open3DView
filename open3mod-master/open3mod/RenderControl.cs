///////////////////////////////////////////////////////////////////////////////////
// Open 3D Model Viewer (open3mod) (v0.1)
// [RenderControl.cs]
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
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using OpenTK;
using OpenTK.Graphics;

namespace open3mod
{
    /// <summary>
    /// Dummy derivative of GLControl to be able to specify constructor
    /// parameters while still being usable with the WinForms designer.
    /// 
    /// The RenderControl always requests a stencil buffer and a 24 bit depth
    /// buffer, which should be natively supported by most hardware in use today.
    /// 
    /// MultiSampling is requested according to the current value of
    /// GraphicsSettings.Default.UseMultiSampling.
    /// 
    /// </summary>
    class RenderControl : GLControl
    {
        public RenderControl()
            : base(new GraphicsMode(new ColorFormat(32), 24, 8, GetSampleCount(GraphicsSettings.Default.MultiSampling)))
        { }


        /// <summary>
        /// Converts a value for GraphicsSettings.Default.MultiSampling into a device-specific
        /// sample count.
        /// </summary>
        /// <param name="multiSampling">Device-independent quality level in [0,3]</param>
        /// <returns>Sample count for device</returns>
        private static int GetSampleCount(int multiSampling)
        {
            // UI names:
            /*  None
                Slight
                Normal
                Maximum
            */
            switch (multiSampling)
            {
                case 0:
                    return 0;
                case 1:
                    return 2;
                case 2:
                    return 4;
                case 3: 
                    return MaximumSampleCount();
                
            }
            Debug.Assert(false);
            return 0;
        }


        /// <summary>
        /// Determines the maximum number of FSAA samples supported by the hardware.
        /// </summary>
        /// <returns></returns>
        private static int MaximumSampleCount()
        {
            // http://www.opentk.com/node/2355 modified to actually work
            var highest = 0;
            var aa = 0;
            do
            {
                var mode = new GraphicsMode(32, 0, 0, aa);
                if(mode.Samples == aa && mode.Samples > highest)
                {
                    highest = mode.Samples;
                }
                aa += 2;
            } while (aa <= 32);
            return highest;
        }
    }
}

/* vi: set shiftwidth=4 tabstop=4: */ 