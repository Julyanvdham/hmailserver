﻿// Copyright (c) 2010 Martin Knafve / hMailServer.com.  
// http://www.hmailserver.com

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VMwareIntegration.Common;

namespace VMwareIntegration.Console
{
   class Program
   {
      private static string _logFile;
      private static object _outputLogLock = new object();

      private static object _lockCounterTest = new object();

      static int Main(string[] args)
      {
         var softwareUnderTest = args[0];

         _logFile = args[1];
         _logFile = _logFile.Replace("%TIMESTAMP%", DateTime.Now.ToString("yyyy-MM-dd HHmmss"));

         if (!File.Exists(softwareUnderTest))
         {
            LogText(string.Format("The file {0} does not exist.", softwareUnderTest));
            return -1;
         }

         LogText("Loading test suite...");

         // Load static container of all tests.
         List<TestEnvironment> listEnvironments = new List<TestEnvironment>();
         TestEnvironments.AddAll(listEnvironments);

         int testIndex = 0;

         var f = TaskScheduler.Default;

         var options = new ParallelOptions()
            {
               MaxDegreeOfParallelism = 3,
            };

         // We can run tests on XP and Vista/2003/2008 at the same time since it's separate VMware images.
         var environmentsGroupedByVmwareImage = listEnvironments.GroupBy(item => item.VMwarePath).ToList();

         var partitioner = Partitioner.Create(environmentsGroupedByVmwareImage, EnumerablePartitionerOptions.NoBuffering); 
         
         Parallel.ForEach(partitioner, options, environmentGroup =>
         {
            foreach (var environment in environmentGroup)
            {
               int localIndex;

               lock (_lockCounterTest)
               {
                  localIndex = ++testIndex;

                  string message =
                     string.Format("{0}: {1}/{2} - Test: {3} on {4} with db {5}. Image: {6} (Snapshot: {7})",
                        DateTime.Now,
                        localIndex,
                        listEnvironments.Count,
                        environment.Description,
                        environment.OperatingSystem,
                        environment.DatabaseType,
                        Path.GetFileName(environment.VMwarePath),
                        environment.SnapshotName);

                  LogText(message);
               }

               var runner = new TestRunner(true, environment, false, softwareUnderTest);

               try
               {
                  runner.Run();
                  
                  LogText(string.Format("{0}: Test {1} completed successfully.",DateTime.Now, localIndex));
               }
               catch (Exception ex) 
               {
                  LogText(string.Format("{0}: Test {1} failed.", DateTime.Now, localIndex));
                  LogText(ex.ToString());
               }
            }

         });
         
         System.Console.WriteLine("All tests completed. Press Enter to exit.");

         System.Console.ReadLine();
         return 0;
      }

      private static void LogText(string text)
      {
         lock (_outputLogLock)
         {
            System.Console.WriteLine(text);
            File.AppendAllText(_logFile, text + Environment.NewLine);
         }
      }
   }
}
