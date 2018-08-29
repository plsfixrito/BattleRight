using System;

namespace Poloma
{
    public class Poloma2 : IPlugin
    {
        public void Load()
        {

        }

        public void UnLoad()
        {
            GC.SuppressFinalize(this);
        }
    }
}
