using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocumentTranslationService.Core
{

    public class FileFormatList
    {
        public Value[] value { get; set; }
    }

    public class Value
    {
        public string format { get; set; }
        public string[] fileExtensions { get; set; }
        public string[] contentTypes { get; set; }
        public string[] versions { get; set; }
    }

}
