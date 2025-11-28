using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.IO;
using System.Xml;

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
      private string executionPath;
      private string name;
      private string produktName;
      private string companyName;


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


            object[] attribsProduct = asm.GetCustomAttributes(typeof(AssemblyProductAttribute), true);
            if (attribsProduct.Length > 0)
            {
               AssemblyProductAttribute asmProduct = attribsProduct[0] as AssemblyProductAttribute;
               produktName = asmProduct.Product.ToString();
            }

            object[] attribsCompany = asm.GetCustomAttributes(typeof(AssemblyCompanyAttribute), true);
            if (attribsCompany.Length > 0)
            {
               AssemblyCompanyAttribute asmCompany = attribsCompany[0] as AssemblyCompanyAttribute;
               companyName = asmCompany.Company.ToString();
            }

            executionPath = asm.Location;
            name = asmName.Name;
            major = asmName.Version.Major;
            minor = asmName.Version.Minor;
            build = asmName.Version.Build;
            revision = asmName.Version.Revision;

            int daysSince2000 = asmName.Version.Build;
            int timeInfo = asmName.Version.Revision;

            fileCompileTime = new DateTime(2000, 1, 1);
            fileCompileTime = fileCompileTime + new TimeSpan(daysSince2000, 0, 0, 2 * timeInfo);

            System.IO.FileInfo fileInfo = new FileInfo(executionPath);
            fileCreationTime = fileInfo.CreationTime;
            fileModifiedTime = fileInfo.LastWriteTime;
         }
         catch
         {
         }
      }
   }
}
