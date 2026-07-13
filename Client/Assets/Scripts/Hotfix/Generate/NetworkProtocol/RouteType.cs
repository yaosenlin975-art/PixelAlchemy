using System.Collections.Generic;
namespace Fantasy
{
    /// <summary>
    /// 本代码有编辑器生成,请不要再这里进行编辑。
    /// Route协议定义(需要定义1000以上、因为1000以内的框架预留)
    /// </summary>
    public static partial class RouteType
    {
        public const int ChatRouteType = 10001; // ChatRouteType
        public const int GateRouteType = 10002; // GateRouteType
        public const int LoginRouteType = 10003; // LoginRouteType
        public const int MatchRouteType = 10004; // MatchRouteType
        public const int GameRouteType = 10005; // GameRouteType
        public const int RealmRouteType = 10006; // RealmRouteType
        public const int ReplayRouteType = 10007; // ReplayRouteType
        public const int AnalyticsRouteType = 10008; // AnalyticsRouteType

        public static IEnumerable<int> RoamingTypes
        {
            get
            {
                yield return ChatRouteType;
                yield return GateRouteType;
                yield return LoginRouteType;
                yield return MatchRouteType;
                yield return GameRouteType;
                yield return RealmRouteType;
                yield return ReplayRouteType;
                yield return AnalyticsRouteType;
            }
        }
    }
}