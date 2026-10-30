namespace NKaspa
{
    /// <summary>
    /// Sigop and script-unit budget for one input.
    /// A failed charge leaves the meter where it was.
    /// </summary>
    internal sealed class RuntimeResourceMeter
    {
        private readonly ulong _sigOpUnits;
        private readonly ulong _limit;
        private ulong _remaining;
        private ushort _usedSigOps;

        internal RuntimeResourceMeter(ulong sigOpUnits, ulong scriptUnitsLimit)
        {
            _sigOpUnits = sigOpUnits;
            _limit = scriptUnitsLimit;
            _remaining = scriptUnitsLimit;
        }

        internal ushort UsedSigOps
        {
            get { return _usedSigOps; }
        }

        internal ulong UsedScriptUnits
        {
            get { return _limit - _remaining; }
        }

        internal ScriptError? Consume(ulong units)
        {
            if (units > _remaining)
            {
                var overflow = units - _remaining;
                var used = ScriptLimits.SaturatingAdd(_limit, overflow);
                return ScriptError.Exceeded(used, _limit);
            }

            _remaining -= units;
            return null;
        }

        internal ScriptError? ConsumeSigOp(int count)
        {
            var units = SaturatingMul((ulong)count, _sigOpUnits);
            var error = Consume(units);
            if (error != null)
            {
                return error;
            }

            var sum = (int)_usedSigOps + count;
            _usedSigOps = sum > ushort.MaxValue ? ushort.MaxValue : (ushort)sum;
            return null;
        }

        internal ScriptError? ChargePushed(ulong bytes)
        {
            return Consume(bytes);
        }

        private static ulong SaturatingMul(ulong left, ulong right)
        {
            if (left == 0 || right == 0)
            {
                return 0;
            }

            return left > ulong.MaxValue / right ? ulong.MaxValue : left * right;
        }
    }
}
