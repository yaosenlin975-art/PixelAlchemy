// 职责：定义像素世界中可用的材料编号，供模拟、渲染和输入统一引用。
// Responsibility: Defines shared material identifiers used by simulation, rendering, and input.
namespace AOT
{
    public enum MaterialType
    {
        Air = 0,
        Empty = Air,
        Sand = 1,
        Water = 2,
        Smoke = 3,
        Fire = 4,
        Stone = 5,
        Wood = 6,
        Ash = 7,
        Poison = 8,
        Ice = 9,
        Lava = 10,
        Debris = 11
    }
}
