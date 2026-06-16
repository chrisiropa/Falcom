using System;
using System.IO;
using System.Reflection;

namespace Falcom
{
   public class AssemblyInfoWrapper
   {
      private int major;
      private int minor;
      private int build;
      private int revision;
      private DateTime fileCompileTime;
      private DateTime fileCreationTime;
      private DateTime fileModifiedTime;
      private string executionPath = string.Empty;
      private string name = string.Empty;
      private string produktName = string.Empty;
      private string companyName = string.Empty;

      public int Major { get { return major; } }
      public int Minor { get { return minor; } }
      public int Build { get { return build; } }
      public int Revision { get { return revision; } }
      public DateTime FileCompileTime { get { return fileCompileTime; } }
      public DateTime FileCreationTime { get { return fileCreationTime; } }
      public DateTime FileModifiedTime { get { return fileModifiedTime; } }
      public string ExecutionPath { get { return executionPath; } }
      public string Name { get { return name; } }
      public string ProduktName { get { return produktName; } }
      public string CompanyName { get { return companyName; } }

      public AssemblyInfoWrapper()
      {
         try
         {
            Assembly asm = Assembly.GetExecutingAssembly();
            AssemblyName asmName = asm.GetName();
            Version? version = asmName.Version;

            object[] attribsProduct = asm.GetCustomAttributes(typeof(AssemblyProductAttribute), true);
            if (attribsProduct.Length > 0 && attribsProduct[0] is AssemblyProductAttribute asmProduct)
            {
               produktName = asmProduct.Product ?? string.Empty;
            }

            object[] attribsCompany = asm.GetCustomAttributes(typeof(AssemblyCompanyAttribute), true);
            if (attribsCompany.Length > 0 && attribsCompany[0] is AssemblyCompanyAttribute asmCompany)
            {
               companyName = asmCompany.Company ?? string.Empty;
            }

            executionPath = asm.Location;
            name = asmName.Name ?? string.Empty;

            if (version is null)
            {
               return;
            }

            major = version.Major;
            minor = version.Minor;
            build = version.Build;
            revision = version.Revision;

            int daysSince2000 = version.Build;
            int timeInfo = version.Revision;

            fileCompileTime = new DateTime(2000, 1, 1);
            fileCompileTime = fileCompileTime + new TimeSpan(daysSince2000, 0, 0, 2 * timeInfo);

            FileInfo fileInfo = new FileInfo(executionPath);
            fileCreationTime = fileInfo.CreationTime;
            fileModifiedTime = fileInfo.LastWriteTime;
         }
         catch
         {
         }
      }
   }
}
