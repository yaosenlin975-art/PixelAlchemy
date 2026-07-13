using System.Runtime.CompilerServices;
using Fantasy;
using Fantasy.Async;
using Fantasy.Network;
using System.Collections.Generic;
#pragma warning disable CS8618
namespace Fantasy
{
   public static class NetworkProtocolHelper
   {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void C2G_TestMessage(this Session session, C2G_TestMessage C2G_TestMessage_message)
		{
			session.Send(C2G_TestMessage_message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void C2G_TestMessage(this Session session, string tag)
		{
			using var C2G_TestMessage_message = Fantasy.C2G_TestMessage.Create();
			C2G_TestMessage_message.Tag = tag;
			session.Send(C2G_TestMessage_message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_TestResponse> C2G_TestRequest(this Session session, C2G_TestRequest C2G_TestRequest_request)
		{
			return (G2C_TestResponse)await session.Call(C2G_TestRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static async FTask<G2C_TestResponse> C2G_TestRequest(this Session session, string tag)
		{
			using var C2G_TestRequest_request = Fantasy.C2G_TestRequest.Create();
			C2G_TestRequest_request.Tag = tag;
			return (G2C_TestResponse)await session.Call(C2G_TestRequest_request);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void C2G_Input(this Session session, C2G_Input C2G_Input_message)
		{
			session.Send(C2G_Input_message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void C2G_Input(this Session session, ulong frame, InputPayload payload)
		{
			using var C2G_Input_message = Fantasy.C2G_Input.Create();
			C2G_Input_message.Frame = frame;
			C2G_Input_message.Payload = payload;
			session.Send(C2G_Input_message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void G2C_FrameBatch(this Session session, G2C_FrameBatch G2C_FrameBatch_message)
		{
			session.Send(G2C_FrameBatch_message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void G2C_FrameBatch(this Session session, ulong frame, List<PlayerInput> inputs)
		{
			using var G2C_FrameBatch_message = Fantasy.G2C_FrameBatch.Create();
			G2C_FrameBatch_message.Frame = frame;
			G2C_FrameBatch_message.Inputs = inputs;
			session.Send(G2C_FrameBatch_message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void G2C_SpectatorBatch(this Session session, G2C_SpectatorBatch G2C_SpectatorBatch_message)
		{
			session.Send(G2C_SpectatorBatch_message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void G2C_SpectatorBatch(this Session session, ulong frame, List<PlayerInput> inputs, uint spectatorId)
		{
			using var G2C_SpectatorBatch_message = Fantasy.G2C_SpectatorBatch.Create();
			G2C_SpectatorBatch_message.Frame = frame;
			G2C_SpectatorBatch_message.Inputs = inputs;
			G2C_SpectatorBatch_message.SpectatorId = spectatorId;
			session.Send(G2C_SpectatorBatch_message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void G2C_MatchSettle(this Session session, G2C_MatchSettle G2C_MatchSettle_message)
		{
			session.Send(G2C_MatchSettle_message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void G2C_MatchSettle(this Session session, uint matchId, uint totalFrames, List<SettlePlayer> players, uint yourselfRank)
		{
			using var G2C_MatchSettle_message = Fantasy.G2C_MatchSettle.Create();
			G2C_MatchSettle_message.MatchId = matchId;
			G2C_MatchSettle_message.TotalFrames = totalFrames;
			G2C_MatchSettle_message.Players = players;
			G2C_MatchSettle_message.YourselfRank = yourselfRank;
			session.Send(G2C_MatchSettle_message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void C2G_DeviceReport(this Session session, C2G_DeviceReport C2G_DeviceReport_message)
		{
			session.Send(C2G_DeviceReport_message);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void C2G_DeviceReport(this Session session, string deviceId, string installId)
		{
			using var C2G_DeviceReport_message = Fantasy.C2G_DeviceReport.Create();
			C2G_DeviceReport_message.DeviceId = deviceId;
			C2G_DeviceReport_message.InstallId = installId;
			session.Send(C2G_DeviceReport_message);
		}

   }
}