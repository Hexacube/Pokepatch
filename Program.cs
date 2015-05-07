using System;
using System.Windows.Forms;
using System.Threading.Tasks;

namespace Pokepatch
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        internal static void Main()
        {
            Application.SetCompatibleTextRenderingDefault(false);
            Application.EnableVisualStyles();
            Application.Run(new Mainform());
        }
    }
}