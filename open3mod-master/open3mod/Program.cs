///////////////////////////////////////////////////////////////////////////////////
// Open 3D Model Viewer (open3mod) (v0.1)
// [Program.cs]
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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace open3mod
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            MainWindow mainWindow = null;
            RunOnceGuard.Guard("open3mod_global_app",

                    // what to do if this is the first instance of the application
                    () =>
                    {
                        Application.EnableVisualStyles();
                        Application.SetCompatibleTextRenderingDefault(false);

                        mainWindow = new MainWindow();
                        if (args.Length > 0)
                        {
                            mainWindow.AddTab(args[0]);
                        }
                        Application.Run(mainWindow);
                        mainWindow = null;

                        TextureQueue.Terminate();
                    },

                    // what do invoke if this is the first instance of the application,
                    // and another (temporary) instance messages it to open a new tab
                    (String absPath) =>
                    {
                        if (mainWindow != null)
                        {
                            mainWindow.BeginInvoke(new MethodInvoker(() =>
                            {
                                mainWindow.Activate();
                                mainWindow.AddTab(absPath);
                            }));                        
                        }
                    },

                    // what to send to the first instance of the application if the
                    // current instance is only temporary.
                    () =>
                    {
                        if(args.Length == 0)
                        {
                            return null;
                        }
                        // note: have to get absolute path because the working dirs
                        // of the instances may be different.
                        return Path.GetFullPath(args[0]);
                    }
                );

            
        }
    }
}

/* vi: set shiftwidth=4 tabstop=4: */ 