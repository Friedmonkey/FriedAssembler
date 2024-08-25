using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Configuration.Install;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;

namespace FbuildSetupInstallerClassHolder
{
    [RunInstaller(true)]
    public partial class InstallerClass : System.Configuration.Install.Installer
    {
        public InstallerClass()
        {
            InitializeComponent();
        }
        public override void Install(IDictionary stateSaver)
        {
            try
            {
                base.Install(stateSaver);

                string path = this.Context.Parameters["targetdir"];
                path = path.Substring(0, path.Length - 1);

                //File.WriteAllText(Path.Combine(path,"hiGuys.txt"),"hello world");

                this.SetEnvironmentVarible("Path", path, false);
                this.SetEnvironmentVarible("Include", Path.Combine(path, "INCLUDE"), false);
            }
            catch 
            {
                this.Context.LogMessage("error occured, sorry");
            }
        }
        public override void Uninstall(IDictionary savedState)
        {
            try
            {
                base.Uninstall(savedState);
                string path = this.Context.Parameters["targetdir"];
                path = path.Substring(0, path.Length - 1);

                this.SetEnvironmentVarible("Path", path, true);
                this.SetEnvironmentVarible("Include", Path.Combine(path, "INCLUDE"), true);
            }
            catch
            {
                this.Context.LogMessage("error occured, sorry");
            }
        }

        public bool hasAdmin()
        {
            bool isElevated;
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                isElevated = principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            return isElevated;
        }

        public void SetEnvironmentVarible(string varible, string FriedPath, bool remove)
        {
            //get the paths AND make a backup
            string paths = Environment.GetEnvironmentVariable(varible, EnvironmentVariableTarget.Machine);
            string date = DateTime.Now.ToString("g").Replace("/", "-").Replace(":", ";");

            var backupPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, varible+"Backups");

            if (!Directory.Exists(backupPath))
            {
                Directory.CreateDirectory(backupPath);
            }

            File.WriteAllText($"{backupPath}\\Backup_{date}.txt", paths);



            if (remove)
            {
                this.Context.LogMessage("removing path varible: " + FriedPath);
                string newPaths = paths.Replace(FriedPath, "");
                Context.LogMessage(newPaths);

                //Directory.Delete(FriedPath, true);
                Environment.SetEnvironmentVariable(varible, newPaths, EnvironmentVariableTarget.Machine);
            }
            else
            {
                this.Context.LogMessage("adding path varible: " + FriedPath);
                string newPaths = $"{paths};{FriedPath}";
                Context.LogMessage(newPaths);

                Environment.SetEnvironmentVariable(varible, newPaths, EnvironmentVariableTarget.Machine);
                //Directory.CreateDirectory(FriedPath);
            }
        }
    }
}
