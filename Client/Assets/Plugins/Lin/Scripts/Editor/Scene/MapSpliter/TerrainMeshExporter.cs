/*
┌────────────────────────────┐
│　Description: Terrain转换Mesh
└────────────────────────────┘
*/
/*using AmazingAssets.TerrainToMesh;
using Lin.Runtime.Helper;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Lin.Editor.Scene.Spliter
{
    public static class TerrainMeshExporter
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="terrainData">目标Terrain</param>
        /// <param name="outputDirectory">纹理, 网格, 材质 的输出文件夹</param>
        /// <param name="vertexCount">行列定点数 (数字越大顶点越多, 三角形越多, 越逼近原Terrain)</param>
        /// <param name="resolution">纹理的大小</param>
        /// <param name="deepth">分拆深度(Terrain会被拆为 4^deepth 块)</param>
        public static void Translate2Meshes(this TerrainData terrainData, string outputDirectory, Transform chunkParent, int vertexCount = 100, int resolution = 512, int deepth = 4, bool exportHoles = false)
        {
            var translator = terrainData.TerrainToMesh();
            var chunkCount = (int)Mathf.Pow(4, deepth);
            IOHelper.InsureExist(outputDirectory, false, true);
            for (int h = 0; h < deepth; h++)
            {
                for (int v = 0; v < deepth; v++)
                {
                    GameObject chunk = new GameObject($"{terrainData.name} [{h}, {v}]");
                    var mesh = translator.ExportMesh(vertexCount, vertexCount, chunkCount, chunkCount, h, v, true);
                    
                    //Diffuse textures's alpha channel contains holesmap cutout, if 'exportHoles' is enabled
                    Texture2D diffuseTexture = translator.ExportBasemapDiffuseTexture(resolution, exportHoles, chunkCount, chunkCount, h, v);
                    Texture2D normalTexture = translator.ExportBasemapNormalTexture(resolution, false, chunkCount, chunkCount, h, v);
                    //Contains metallic(R), occlusion(G) and smoothness(A)
                    Texture2D maskTexture = translator.ExportBasemapMaskTexture(resolution, chunkCount, chunkCount, h, v);                   
                    Texture2D occlusionTexture = translator.ExportBasemapOcclusionTexture(resolution, chunkCount, chunkCount, h, v);
                    
                    string mainName = $"{Path.GetFileNameWithoutExtension(chunk.name)}_{chunk.name}";
                    Material material = new Material(TerrainToMeshUtilities.GetDefaultShader());
                    material.name = mainName;
                    TerrainToMeshUtilities.SetupDefaultMaterial(material, diffuseTexture, exportHoles, normalTexture, maskTexture, occlusionTexture);

                    //textures
                    Save(diffuseTexture, "diffuse", "png");
                    Save(normalTexture, "normal", "png");
                    Save(maskTexture, "mask", "png");
                    Save(occlusionTexture, "occlusion", "png");
                    //mesh
                    Save(mesh, "mesh", "asset");
                    //material
                    Save(material, "material", "mat");

                    chunk.AddComponent<MeshFilter>().sharedMesh = mesh;
                    chunk.AddComponent<MeshRenderer>().sharedMaterial = material;
                    chunk.transform.SetParent(chunkParent);

                    void Save(Object target, string name, string type) => AssetDatabase.CreateAsset(target, $"{outputDirectory}/{mainName}_{name}_{h}_{v}.{type}");
                }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}*/