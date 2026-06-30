using NoitaCA;
using NUnit.Framework;
using UnityEngine;

public sealed class PixelWorldInteractionTests
{
    [Test]
    public void RendererLegacyInitializeStillBuildsTexture()
    {
        GameObject gameObject = new GameObject("Renderer Test");
        try
        {
            PixelWorldRenderer renderer = gameObject.AddComponent<PixelWorldRenderer>();
            PixelGrid grid = new PixelGrid(6, 4);
            grid.SetMaterial(2, 1, MaterialType.Stone);

            renderer.Initialize(grid, 8);
            renderer.Render();

            SpriteRenderer spriteRenderer = gameObject.GetComponent<SpriteRenderer>();
            Assert.IsNotNull(spriteRenderer.sprite);
            Assert.AreEqual(new Vector2(0.75f, 0.5f), renderer.WorldSize);
            Assert.AreEqual(6, spriteRenderer.sprite.texture.width);
            Assert.AreEqual(4, spriteRenderer.sprite.texture.height);
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void VisualColorEvaluationIsDeterministic()
    {
        PixelWorldRenderSettings settings = PixelWorldRenderSettings.CreateAlchemyDefault();
        Pixel pixel = Pixel.FromMaterial(MaterialType.Stone);

        Color32 first = PixelWorldRenderer.EvaluateVisualColor(pixel, 11, 7, 32, 24, settings);
        Color32 second = PixelWorldRenderer.EvaluateVisualColor(pixel, 11, 7, 32, 24, settings);

        Assert.AreEqual(first, second);
    }

    [Test]
    public void AirVisualColorStaysDark()
    {
        PixelWorldRenderSettings settings = PixelWorldRenderSettings.CreateAlchemyDefault();
        Color32 color = PixelWorldRenderer.EvaluateVisualColor(Pixel.FromMaterial(MaterialType.Air), 12, 18, 32, 32, settings);

        Assert.Less(color.r, 48);
        Assert.Less(color.g, 56);
        Assert.Less(color.b, 72);
    }

    [Test]
    public void SpawnPointClampsNegativeWeight()
    {
        PixelWorldSpawnPoint spawnPoint = new PixelWorldSpawnPoint(
            PixelWorldSpawnCategory.Monster,
            new Vector2Int(4, 5),
            Vector3.one,
            2,
            -3f);

        Assert.AreEqual(0f, spawnPoint.Weight);
        Assert.AreEqual(2, spawnPoint.SegmentIndex);
    }

    [Test]
    public void PlayerWarpToCellUsesRendererCoordinates()
    {
        GameObject worldObject = new GameObject("World Renderer Test");
        GameObject playerObject = new GameObject("Player Warp Test");
        try
        {
            PixelGrid grid = new PixelGrid(32, 24);
            PixelWorldRenderer renderer = worldObject.AddComponent<PixelWorldRenderer>();
            renderer.Initialize(grid, 8);

            PlayerController controller = playerObject.AddComponent<PlayerController>();
            controller.Initialize(grid, renderer, null, new Vector2Int(4, 6), null);
            controller.WarpToCell(new Vector2Int(11, 9), true);

            Assert.AreEqual(new Vector2Int(11, 9), renderer.WorldToCell(controller.transform.position));
        }
        finally
        {
            Object.DestroyImmediate(playerObject);
            Object.DestroyImmediate(worldObject);
        }
    }

    [Test]
    public void DestroyCircleClearsOnlyCellsInsideRadius()
    {
        PixelGrid grid = new PixelGrid(16, 16);
        for (int y = 0; y < grid.Height; y++)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                grid.SetMaterial(x, y, MaterialType.Stone);
            }
        }

        int changed = grid.DestroyCircle(new Vector2Int(8, 8), 3);

        Assert.Greater(changed, 0);
        Assert.AreEqual(MaterialType.Air, grid.GetMaterial(8, 8));
        Assert.AreEqual(MaterialType.Stone, grid.GetMaterial(0, 0));
        Assert.AreEqual(MaterialType.Stone, grid.GetMaterial(12, 8));
    }

    [Test]
    public void SpawnMaterialPlacesRequestedMaterialWithinBounds()
    {
        PixelGrid grid = new PixelGrid(20, 20);

        int placed = grid.SpawnMaterial(new Vector2Int(10, 10), MaterialType.Poison, 24, 4);

        Assert.Greater(placed, 0);
        Assert.LessOrEqual(placed, 24);
        Assert.Greater(CountMaterial(grid, MaterialType.Poison), 0);
    }

    [Test]
    public void IsSolidMatchesPlayerBlockingMaterials()
    {
        PixelGrid grid = new PixelGrid(8, 8);

        grid.SetMaterial(1, 1, MaterialType.Stone);
        grid.SetMaterial(2, 1, MaterialType.Wood);
        grid.SetMaterial(3, 1, MaterialType.Ice);
        grid.SetMaterial(1, 2, MaterialType.Water);
        grid.SetMaterial(2, 2, MaterialType.Smoke);
        grid.SetMaterial(3, 2, MaterialType.Fire);
        grid.SetMaterial(4, 2, MaterialType.Poison);

        Assert.IsTrue(grid.IsSolid(1, 1));
        Assert.IsTrue(grid.IsSolid(2, 1));
        Assert.IsTrue(grid.IsSolid(3, 1));
        Assert.IsFalse(grid.IsSolid(1, 2));
        Assert.IsFalse(grid.IsSolid(2, 2));
        Assert.IsFalse(grid.IsSolid(3, 2));
        Assert.IsFalse(grid.IsSolid(4, 2));
    }

    [Test]
    public void ExplodeCircleClearsTerrainAndAddsFeedbackMaterials()
    {
        PixelGrid grid = new PixelGrid(24, 24);
        grid.PaintCircle(12, 12, 5, MaterialType.Stone);

        grid.ExplodeCircle(new Vector2Int(12, 12), 4);

        Assert.AreEqual(MaterialType.Air, grid.GetMaterial(12, 12));
        Assert.Greater(CountMaterial(grid, MaterialType.Smoke), 0);
        Assert.Greater(CountMaterial(grid, MaterialType.Fire), 0);
        Assert.Greater(CountMaterial(grid, MaterialType.Debris), 0);
    }

    private static int CountMaterial(PixelGrid grid, MaterialType materialType)
    {
        int count = 0;
        for (int y = 0; y < grid.Height; y++)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                if (grid.GetMaterial(x, y) == materialType)
                {
                    count++;
                }
            }
        }

        return count;
    }
}
