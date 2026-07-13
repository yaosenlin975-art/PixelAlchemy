using LightProto;
using System;
using MemoryPack;
using System.Collections.Generic;
using Fantasy;
using Fantasy.Pool;
using Fantasy.Network.Interface;
using Fantasy.Serialize;

#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning disable CS8618
// ReSharper disable InconsistentNaming
// ReSharper disable CollectionNeverUpdated.Global
// ReSharper disable RedundantTypeArgumentsOfMethod
// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable PreferConcreteValueOverDefault
// ReSharper disable RedundantNameQualifier
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable CheckNamespace
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable RedundantUsingDirective
// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
namespace Fantasy
{
    [Serializable]
    [ProtoContract]
    public partial class C2G_TestMessage : AMessage, IMessage
    {
        public static C2G_TestMessage Create(bool autoReturn = true)
        {
            var c2G_TestMessage = MessageObjectPool<C2G_TestMessage>.Rent();
            c2G_TestMessage.AutoReturn = autoReturn;
            
            if (!autoReturn)
            {
                c2G_TestMessage.SetIsPool(false);
            }
            
            return c2G_TestMessage;
        }
        
        public void Return()
        {
            if (!AutoReturn)
            {
                SetIsPool(true);
                AutoReturn = true;
            }
            else if (!IsPool())
            {
                return;
            }
            Dispose();
        }

        public void Dispose()
        {
            if (!IsPool()) return; 
            Tag = default;
            MessageObjectPool<C2G_TestMessage>.Return(this);
        }
        public uint OpCode() { return OuterOpcode.C2G_TestMessage; } 
        [ProtoMember(1)]
        public string Tag { get; set; }
    }
    [Serializable]
    [ProtoContract]
    public partial class C2G_TestRequest : AMessage, IRequest
    {
        public static C2G_TestRequest Create(bool autoReturn = true)
        {
            var c2G_TestRequest = MessageObjectPool<C2G_TestRequest>.Rent();
            c2G_TestRequest.AutoReturn = autoReturn;
            
            if (!autoReturn)
            {
                c2G_TestRequest.SetIsPool(false);
            }
            
            return c2G_TestRequest;
        }
        
        public void Return()
        {
            if (!AutoReturn)
            {
                SetIsPool(true);
                AutoReturn = true;
            }
            else if (!IsPool())
            {
                return;
            }
            Dispose();
        }

        public void Dispose()
        {
            if (!IsPool()) return; 
            Tag = default;
            MessageObjectPool<C2G_TestRequest>.Return(this);
        }
        public uint OpCode() { return OuterOpcode.C2G_TestRequest; } 
        [ProtoIgnore]
        public G2C_TestResponse ResponseType { get; set; }
        [ProtoMember(1)]
        public string Tag { get; set; }
    }
    [Serializable]
    [ProtoContract]
    public partial class G2C_TestResponse : AMessage, IResponse
    {
        public static G2C_TestResponse Create(bool autoReturn = true)
        {
            var g2C_TestResponse = MessageObjectPool<G2C_TestResponse>.Rent();
            g2C_TestResponse.AutoReturn = autoReturn;
            
            if (!autoReturn)
            {
                g2C_TestResponse.SetIsPool(false);
            }
            
            return g2C_TestResponse;
        }
        
        public void Return()
        {
            if (!AutoReturn)
            {
                SetIsPool(true);
                AutoReturn = true;
            }
            else if (!IsPool())
            {
                return;
            }
            Dispose();
        }

        public void Dispose()
        {
            if (!IsPool()) return; 
            ErrorCode = 0;
            Tag = default;
            MessageObjectPool<G2C_TestResponse>.Return(this);
        }
        public uint OpCode() { return OuterOpcode.G2C_TestResponse; } 
        [ProtoMember(2)]
        public uint ErrorCode { get; set; }
        [ProtoMember(1)]
        public string Tag { get; set; }
    }
    [Serializable]
    [MemoryPackable]
    public partial class C2G_Input : AMessage, IMessage
    {
        public static C2G_Input Create(bool autoReturn = true)
        {
            var c2G_Input = MessageObjectPool<C2G_Input>.Rent();
            c2G_Input.AutoReturn = autoReturn;
            
            if (!autoReturn)
            {
                c2G_Input.SetIsPool(false);
            }
            
            return c2G_Input;
        }
        
        public void Return()
        {
            if (!AutoReturn)
            {
                SetIsPool(true);
                AutoReturn = true;
            }
            else if (!IsPool())
            {
                return;
            }
            Dispose();
        }

        public void Dispose()
        {
            if (!IsPool()) return; 
            Frame = default;
            if (Payload != null)
            {
                Payload.Dispose();
                Payload = null;
            }
            MessageObjectPool<C2G_Input>.Return(this);
        }
        public uint OpCode() { return OuterOpcode.C2G_Input; } 
        [MemoryPackOrder(1)]
        public ulong Frame { get; set; }
        [MemoryPackOrder(2)]
        public InputPayload Payload { get; set; }
    }
    [Serializable]
    [MemoryPackable]
    public partial class InputPayload : AMessage, IDisposable
    {
        public static InputPayload Create(bool autoReturn = true)
        {
            var inputPayload = MessageObjectPool<InputPayload>.Rent();
            inputPayload.AutoReturn = autoReturn;
            
            if (!autoReturn)
            {
                inputPayload.SetIsPool(false);
            }
            
            return inputPayload;
        }
        
        public void Return()
        {
            if (!AutoReturn)
            {
                SetIsPool(true);
                AutoReturn = true;
            }
            else if (!IsPool())
            {
                return;
            }
            Dispose();
        }

        public void Dispose()
        {
            if (!IsPool()) return; 
            MoveX = default;
            MoveY = default;
            ActionFlags = default;
            AimAngle = default;
            MessageObjectPool<InputPayload>.Return(this);
        }
        [MemoryPackOrder(1)]
        public int16 MoveX { get; set; }
        [MemoryPackOrder(2)]
        public int16 MoveY { get; set; }
        [MemoryPackOrder(3)]
        public uint16 ActionFlags { get; set; }
        [MemoryPackOrder(4)]
        public int16 AimAngle { get; set; }
    }
    [Serializable]
    [MemoryPackable]
    public partial class G2C_FrameBatch : AMessage, IMessage
    {
        public static G2C_FrameBatch Create(bool autoReturn = true)
        {
            var g2C_FrameBatch = MessageObjectPool<G2C_FrameBatch>.Rent();
            g2C_FrameBatch.AutoReturn = autoReturn;
            
            if (!autoReturn)
            {
                g2C_FrameBatch.SetIsPool(false);
            }
            
            return g2C_FrameBatch;
        }
        
        public void Return()
        {
            if (!AutoReturn)
            {
                SetIsPool(true);
                AutoReturn = true;
            }
            else if (!IsPool())
            {
                return;
            }
            Dispose();
        }

        public void Dispose()
        {
            if (!IsPool()) return; 
            Frame = default;
            foreach (var __t in Inputs) __t.Dispose();
            Inputs.Clear();
            MessageObjectPool<G2C_FrameBatch>.Return(this);
        }
        public uint OpCode() { return OuterOpcode.G2C_FrameBatch; } 
        [MemoryPackOrder(1)]
        public ulong Frame { get; set; }
        [MemoryPackOrder(2)]
        public List<PlayerInput> Inputs { get; set; } = new List<PlayerInput>();
    }
    [Serializable]
    [MemoryPackable]
    public partial class PlayerInput : AMessage, IDisposable
    {
        public static PlayerInput Create(bool autoReturn = true)
        {
            var playerInput = MessageObjectPool<PlayerInput>.Rent();
            playerInput.AutoReturn = autoReturn;
            
            if (!autoReturn)
            {
                playerInput.SetIsPool(false);
            }
            
            return playerInput;
        }
        
        public void Return()
        {
            if (!AutoReturn)
            {
                SetIsPool(true);
                AutoReturn = true;
            }
            else if (!IsPool())
            {
                return;
            }
            Dispose();
        }

        public void Dispose()
        {
            if (!IsPool()) return; 
            PlayerId = default;
            if (Payload != null)
            {
                Payload.Dispose();
                Payload = null;
            }
            MessageObjectPool<PlayerInput>.Return(this);
        }
        [MemoryPackOrder(1)]
        public uint PlayerId { get; set; }
        [MemoryPackOrder(2)]
        public InputPayload Payload { get; set; }
    }
    [Serializable]
    [MemoryPackable]
    public partial class G2C_SpectatorBatch : AMessage, IMessage
    {
        public static G2C_SpectatorBatch Create(bool autoReturn = true)
        {
            var g2C_SpectatorBatch = MessageObjectPool<G2C_SpectatorBatch>.Rent();
            g2C_SpectatorBatch.AutoReturn = autoReturn;
            
            if (!autoReturn)
            {
                g2C_SpectatorBatch.SetIsPool(false);
            }
            
            return g2C_SpectatorBatch;
        }
        
        public void Return()
        {
            if (!AutoReturn)
            {
                SetIsPool(true);
                AutoReturn = true;
            }
            else if (!IsPool())
            {
                return;
            }
            Dispose();
        }

        public void Dispose()
        {
            if (!IsPool()) return; 
            Frame = default;
            foreach (var __t in Inputs) __t.Dispose();
            Inputs.Clear();
            SpectatorId = default;
            MessageObjectPool<G2C_SpectatorBatch>.Return(this);
        }
        public uint OpCode() { return OuterOpcode.G2C_SpectatorBatch; } 
        [MemoryPackOrder(1)]
        public ulong Frame { get; set; }
        [MemoryPackOrder(2)]
        public List<PlayerInput> Inputs { get; set; } = new List<PlayerInput>();
        [MemoryPackOrder(4)]
        public uint SpectatorId { get; set; }
    }
    [Serializable]
    [ProtoContract]
    public partial class G2C_MatchSettle : AMessage, IMessage
    {
        public static G2C_MatchSettle Create(bool autoReturn = true)
        {
            var g2C_MatchSettle = MessageObjectPool<G2C_MatchSettle>.Rent();
            g2C_MatchSettle.AutoReturn = autoReturn;
            
            if (!autoReturn)
            {
                g2C_MatchSettle.SetIsPool(false);
            }
            
            return g2C_MatchSettle;
        }
        
        public void Return()
        {
            if (!AutoReturn)
            {
                SetIsPool(true);
                AutoReturn = true;
            }
            else if (!IsPool())
            {
                return;
            }
            Dispose();
        }

        public void Dispose()
        {
            if (!IsPool()) return; 
            MatchId = default;
            TotalFrames = default;
            foreach (var __t in Players) __t.Dispose();
            Players.Clear();
            YourselfRank = default;
            MessageObjectPool<G2C_MatchSettle>.Return(this);
        }
        public uint OpCode() { return OuterOpcode.G2C_MatchSettle; } 
        [ProtoMember(1)]
        public uint MatchId { get; set; }
        [ProtoMember(2)]
        public uint TotalFrames { get; set; }
        [ProtoMember(3)]
        public List<SettlePlayer> Players { get; set; } = new List<SettlePlayer>();
        [ProtoMember(4)]
        public uint YourselfRank { get; set; }
    }
    [Serializable]
    [ProtoContract]
    public partial class SettlePlayer : AMessage, IDisposable
    {
        public static SettlePlayer Create(bool autoReturn = true)
        {
            var settlePlayer = MessageObjectPool<SettlePlayer>.Rent();
            settlePlayer.AutoReturn = autoReturn;
            
            if (!autoReturn)
            {
                settlePlayer.SetIsPool(false);
            }
            
            return settlePlayer;
        }
        
        public void Return()
        {
            if (!AutoReturn)
            {
                SetIsPool(true);
                AutoReturn = true;
            }
            else if (!IsPool())
            {
                return;
            }
            Dispose();
        }

        public void Dispose()
        {
            if (!IsPool()) return; 
            PlayerId = default;
            Rank = default;
            Kills = default;
            Deaths = default;
            MaterialContribution = default;
            MmrDelta = default;
            MessageObjectPool<SettlePlayer>.Return(this);
        }
        [ProtoMember(1)]
        public uint PlayerId { get; set; }
        [ProtoMember(2)]
        public uint Rank { get; set; }
        [ProtoMember(3)]
        public uint Kills { get; set; }
        [ProtoMember(4)]
        public uint Deaths { get; set; }
        [ProtoMember(5)]
        public uint MaterialContribution { get; set; }
        [ProtoMember(6)]
        public int MmrDelta { get; set; }
    }
    [Serializable]
    [ProtoContract]
    public partial class C2G_DeviceReport : AMessage, IMessage
    {
        public static C2G_DeviceReport Create(bool autoReturn = true)
        {
            var c2G_DeviceReport = MessageObjectPool<C2G_DeviceReport>.Rent();
            c2G_DeviceReport.AutoReturn = autoReturn;
            
            if (!autoReturn)
            {
                c2G_DeviceReport.SetIsPool(false);
            }
            
            return c2G_DeviceReport;
        }
        
        public void Return()
        {
            if (!AutoReturn)
            {
                SetIsPool(true);
                AutoReturn = true;
            }
            else if (!IsPool())
            {
                return;
            }
            Dispose();
        }

        public void Dispose()
        {
            if (!IsPool()) return; 
            DeviceId = default;
            InstallId = default;
            MessageObjectPool<C2G_DeviceReport>.Return(this);
        }
        public uint OpCode() { return OuterOpcode.C2G_DeviceReport; } 
        [ProtoMember(1)]
        public string DeviceId { get; set; }
        [ProtoMember(2)]
        public string InstallId { get; set; }
    }
}