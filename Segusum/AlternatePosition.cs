using System;

namespace Seg
{
    public class AlternatePosition
    {
        /// <summary>
        /// Historical object-initializer API. New game code can use the constructor.
        /// </summary>
        public AlternatePosition()
        {
        }

        public AlternatePosition(string serId)
        {
            if (string.IsNullOrWhiteSpace(serId))
            {
                throw new ArgumentException("L'ID della posizione alternativa non può essere vuoto.", nameof(serId));
            }

            this.serId = serId;
        }

        /// <summary>
        /// Stable identifier written to savegames and matched with PositionName in layer_data.json.
        /// </summary>
        public string serId = string.Empty;

        public override string ToString()
        {
            return serId;
        }
    }
}
