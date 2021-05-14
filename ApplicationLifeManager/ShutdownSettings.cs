using System;
using System.Collections.Generic;

namespace ApplicationLifeManager
{
   public class ShutdownSettings
   {
      public TimeSpan WaitTimeout { get; set; }
      public IEnumerable<string> IgnorePaths { get; set; }
   }
}