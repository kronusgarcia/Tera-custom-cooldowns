﻿using System;
using Tera.Game;
using Tera.Game.Messages;

namespace TCC.Parsing.Messages
{
    public class S_DUNGEON_EVENT_MESSAGE : ParsedMessage
    {
        public uint MessageId { get; private set; }
        public S_DUNGEON_EVENT_MESSAGE(TeraMessageReader reader) : base(reader)
        {
            var o = reader.ReadUInt16();
            reader.BaseStream.Position = o - 4;
            if (UInt32.TryParse(reader.ReadTeraString().Substring("@dungeon:".Length), out uint msgId)) MessageId = msgId;
            else MessageId = 0;
            
        }
    }
}
