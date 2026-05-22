using System;

namespace NHotkey
{
    public class HotkeyAlreadyRegisteredException : Exception
    {
        private readonly string _name;

        public HotkeyAlreadyRegisteredException(string name, Exception inner) : base(inner.Message, inner)
        {
            _name = name;
        }

        public string Name
        {
            get { return _name; }
        }
    }
}
