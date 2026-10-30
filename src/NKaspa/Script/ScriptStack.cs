namespace NKaspa
{
    /// <summary>One script stack. Index 0 is the bottom. The last item is the top.</summary>
    internal sealed class ScriptStack
    {
        private readonly List<byte[]> _items = new List<byte[]>();
        private ulong _pushedBytes;

        internal int Count
        {
            get { return _items.Count; }
        }

        internal byte[] this[int index]
        {
            get { return _items[index]; }
        }

        internal ulong PopPushedBytes()
        {
            var pushed = _pushedBytes;
            _pushedBytes = 0;
            return pushed;
        }

        internal ScriptStack CloneStack()
        {
            var copy = new ScriptStack();
            for (var i = 0; i < _items.Count; i++)
            {
                copy._items.Add((byte[])_items[i].Clone());
            }

            copy._pushedBytes = _pushedBytes;
            return copy;
        }

        internal void Clear()
        {
            _items.Clear();
        }

        internal ScriptError? Push(byte[] item, bool meter)
        {
            if (item.Length > ScriptLimits.MaxScriptElementSize)
            {
                return ScriptError.ElementTooBig(item.Length, ScriptLimits.MaxScriptElementSize);
            }

            if (meter)
            {
                _pushedBytes = ScriptLimits.SaturatingAdd(_pushedBytes, (ulong)item.Length);
            }

            _items.Add(item);
            return null;
        }

        internal ScriptError? PushNumber(long value, bool meter)
        {
            var error = ScriptNum.TrySerializeI64(value, out var encoded);
            if (error != null)
            {
                return error;
            }

            return Push(encoded, meter);
        }

        internal ScriptError? PushBool(bool value, bool meter)
        {
            return Push(value ? new byte[] { 1 } : Array.Empty<byte>(), meter);
        }

        internal ScriptError? Pop(out byte[] item)
        {
            if (_items.Count == 0)
            {
                item = Array.Empty<byte>();
                return ScriptError.EmptyStack;
            }

            item = _items[_items.Count - 1];
            _items.RemoveAt(_items.Count - 1);
            return null;
        }

        internal ScriptError? PopRaw(int count, out byte[][] items)
        {
            if (_items.Count < count)
            {
                items = Array.Empty<byte[]>();
                return ScriptError.InvalidStackOperation(count, _items.Count);
            }

            items = SplitOff(_items.Count - count);
            return null;
        }

        internal byte[][] SplitOff(int index)
        {
            var taken = new byte[_items.Count - index][];
            for (var i = 0; i < taken.Length; i++)
            {
                taken[i] = _items[index + i];
            }

            _items.RemoveRange(index, taken.Length);
            return taken;
        }

        internal ScriptError? Drop(int count)
        {
            if (_items.Count < count)
            {
                return ScriptError.InvalidStackOperation(count, _items.Count);
            }

            _items.RemoveRange(_items.Count - count, count);
            return null;
        }

        internal ScriptError? Dup(int count)
        {
            if (_items.Count < count)
            {
                return ScriptError.InvalidStackOperation(count, _items.Count);
            }

            var start = _items.Count - count;
            long bytes = 0;
            for (var i = 0; i < count; i++)
            {
                var copy = (byte[])_items[start + i].Clone();
                bytes += copy.Length;
                _items.Add(copy);
            }

            _pushedBytes = ScriptLimits.SaturatingAdd(_pushedBytes, (ulong)bytes);
            return null;
        }

        internal ScriptError? Over(int count)
        {
            if (_items.Count < 2 * count)
            {
                return ScriptError.InvalidStackOperation(2 * count, _items.Count);
            }

            var start = _items.Count - (2 * count);
            long bytes = 0;
            for (var i = 0; i < count; i++)
            {
                var copy = (byte[])_items[start + i].Clone();
                bytes += copy.Length;
                _items.Add(copy);
            }

            _pushedBytes = ScriptLimits.SaturatingAdd(_pushedBytes, (ulong)bytes);
            return null;
        }

        internal ScriptError? Rot(int count)
        {
            if (_items.Count < 3 * count)
            {
                return ScriptError.InvalidStackOperation(3 * count, _items.Count);
            }

            RotateLeft(_items.Count - (3 * count), count);
            return null;
        }

        internal ScriptError? Swap(int count)
        {
            if (_items.Count < 2 * count)
            {
                return ScriptError.InvalidStackOperation(2 * count, _items.Count);
            }

            RotateLeft(_items.Count - (2 * count), count);
            return null;
        }

        internal ScriptError? Roll(int loc)
        {
            if (loc < 0 || loc >= _items.Count)
            {
                return ScriptError.InvalidStackOperation(loc, _items.Count);
            }

            if (loc == 0)
            {
                return null;
            }

            RotateLeft(_items.Count - loc - 1, 1);
            return null;
        }

        internal ScriptError? RemoveSecond()
        {
            if (_items.Count < 2)
            {
                return ScriptError.InvalidStackOperation(2, _items.Count);
            }

            _items.RemoveAt(_items.Count - 2);
            return null;
        }

        internal ScriptError? Tuck()
        {
            if (_items.Count < 2)
            {
                return ScriptError.InvalidStackOperation(2, _items.Count);
            }

            var copy = (byte[])_items[_items.Count - 1].Clone();
            return Insert(_items.Count - 2, copy);
        }

        internal ScriptError? Insert(int index, byte[] item)
        {
            if (item.Length > ScriptLimits.MaxScriptElementSize)
            {
                return ScriptError.ElementTooBig(item.Length, ScriptLimits.MaxScriptElementSize);
            }

            _pushedBytes = ScriptLimits.SaturatingAdd(_pushedBytes, (ulong)item.Length);
            _items.Insert(index, item);
            return null;
        }

        private void RotateLeft(int start, int count)
        {
            var moved = new byte[count][];
            for (var i = 0; i < count; i++)
            {
                moved[i] = _items[start + i];
            }

            var tail = _items.Count - start - count;
            for (var i = 0; i < tail; i++)
            {
                _items[start + i] = _items[start + count + i];
            }

            for (var i = 0; i < count; i++)
            {
                _items[start + tail + i] = moved[i];
            }
        }
    }
}
