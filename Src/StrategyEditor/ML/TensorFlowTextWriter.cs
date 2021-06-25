using System.IO;
using System.Reflection;
using System.Text;
using log4net;

namespace StrategyEditor.ML
{
    internal class TensorFlowTextWriter : TextWriter
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public override void Write(string value)
        {
            Log.Info(value);
        }

        public override void WriteLine(string value)
        {
            Log.Info(value);
        }

        public override Encoding Encoding { get; }
    }
}