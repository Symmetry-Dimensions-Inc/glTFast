// Copyright 2020-2022 Andreas Atteneder
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

#if GLTFAST_BUILTIN_RP || UNITY_EDITOR

using System.Collections.Generic;
using UnityEngine;
using Material = UnityEngine.Material;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GLTFast.Materials {

    using Logging;
    using AlphaMode = Schema.Material.AlphaMode;
   
    /// <summary>
    /// Converts glTF materials to MRTK materials for the Built-in Render Pipeline
    /// </summary>
    public class MrtkMaterialGenerator : MaterialGenerator {

        // Built-in Render Pipeline
        const string TAG_RENDER_TYPE_MRTK_CUTOUT = "Cutout";
        const string TAG_RENDER_TYPE_MRTK_TRANSPARENCY = "Transparency";
        const string KW_ALPHABLEND_ON = "_ALPHABLEND_ON";
        const string KW_ALPHAPREMULTIPLY_ON = "_ALPHAPREMULTIPLY_ON";
        const string KW_EMISSION = "_EMISSION";
        //const string KW_EMISSION_MAP = "_EmissionMap";
        //const string KW_METALLIC_ROUGNESS_MAP = "_METALLICGLOSSMAP";
        //const string KW_OCCLUSION = "_OCCLUSION";
        //const string KW_SPEC_GLOSS_MAP = "_SPECGLOSSMAP";
        const string KW_NORMAL_MAP = "_NORMAL_MAP";

        static readonly int glossinessPropId = Shader.PropertyToID("_Glossiness");
        //static readonly int metallicGlossMapPropId = Shader.PropertyToID("_MetallicGlossMap");
        //static readonly int metallicRoughnessMapScaleTransformPropId = Shader.PropertyToID("_MetallicGlossMap_ST");
        //static readonly int metallicRoughnessMapRotationPropId = Shader.PropertyToID("_MetallicGlossMapRotation");
        //static readonly int metallicRoughnessMapUVChannelPropId = Shader.PropertyToID("_MetallicGlossMapUVChannel");
        static readonly int modePropId = Shader.PropertyToID("_Mode");
        static readonly int smoothnessPropId = Shader.PropertyToID("_Smoothness");
        static readonly int directionLightingPropId = Shader.PropertyToID("_DirectionalLight");
        static readonly int channelMapPropId = Shader.PropertyToID("_ChannelMap");
        static readonly int normalMapPropId = Shader.PropertyToID("_NormalMap");
        static readonly int normalMapScalePropId = Shader.PropertyToID("_NormalMapScale");

#if UNITY_EDITOR
        const string SHADER_ASSET_PATH = "Assets/MRTK/Shaders/MixedRealityStandard.shader";
#else
        const string SHADER_NAME = "Mixed Reality Toolkit/Standard";
#endif

        Shader mrtkStandardShader;

        /// <inheritdoc />
        public override Material GetDefaultMaterial() {
            return CreateStandardMaterial();
        }
        
        /// <summary>
        /// Finds the shader required for metallic/roughness based materials.
        /// </summary>
        /// <returns>Metallic/Roughness shader</returns>
        protected virtual Shader FindMrtkShader() {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<Shader>(SHADER_ASSET_PATH);
#else
            return FindShader(SHADER_NAME);
#endif
        }

        Material CreateStandardMaterial(string materialName=null)
        {
            if(mrtkStandardShader==null)
            {
                mrtkStandardShader = FindMrtkShader();
            }
            if(mrtkStandardShader==null)
            {
                return null;
            }
            var mat = new Material(mrtkStandardShader)
            {
                name = string.IsNullOrEmpty(materialName) ? $"glTF Material (unnamed)" : materialName
            };
            return mat;
        }

/*
        Material GetPbrSpecularGlossinessMaterial(bool doubleSided=false) {
            if(pbrSpecularGlossinessShader==null) {
                pbrSpecularGlossinessShader = FinderShaderSpecularGlossiness();
            }
            if(pbrSpecularGlossinessShader==null) {
                return null;
            }
            var mat = new Material(pbrSpecularGlossinessShader);
            if(doubleSided) {
                // Turn off back-face culling
                mat.SetFloat(cullModePropId,0);
#if UNITY_EDITOR
                mat.doubleSidedGI = true;
#endif
            }
            return mat;
        }

        Material GetUnlitMaterial(bool doubleSided=false) {
            if(unlitShader==null) {
                unlitShader = FinderShaderUnlit();
            }
            if(unlitShader==null) {
                return null;
            }
            var mat = new Material(unlitShader);
            if(doubleSided) {
                // Turn off back-face culling
                mat.SetFloat(cullModePropId,0);
#if UNITY_EDITOR
                mat.doubleSidedGI = true;
#endif
            }
            return mat;
        }*/

        /// <inheritdoc />
        public override Material GenerateMaterial(
            Schema.Material gltfMaterial,
            IGltfReadable gltf
        ) {
            Debug.Log("Generating material");

            Material material = CreateStandardMaterial(
                string.IsNullOrEmpty(gltfMaterial.name) 
                ? $"glTF Material (? of {gltf.materialCount})" 
                : gltfMaterial.name);

            if (material == null)
            {
                Debug.LogWarning("Failed to create material for glTF object");
                return null;
            }

            if (gltfMaterial.extensions?.KHR_materials_unlit != null)
                material.SetFloat(directionLightingPropId, 0.0f);

            Color baseColorLinear = Color.white;

            // Detect base color if using pbrMetallicRoughness
            if (gltfMaterial.pbrMetallicRoughness != null)
            {
                baseColorLinear = gltfMaterial.pbrMetallicRoughness.baseColor;

                if (gltfMaterial.pbrMetallicRoughness.baseColorTexture?.index >= 0)
                {
                    material.mainTexture = gltf.GetImage(gltfMaterial.pbrMetallicRoughness.baseColorTexture.index);
                }

                if (gltfMaterial.pbrMetallicRoughness.metallicRoughnessTexture?.index >= 0)
                {
                    var texture = gltf.GetImage(gltfMaterial.pbrMetallicRoughness.metallicRoughnessTexture.index);

                    Texture2D occlusionTexture = null;
                    if (gltfMaterial.occlusionTexture.index >= 0)
                    {
                        occlusionTexture = gltf.GetImage(gltfMaterial.occlusionTexture.index);
                    }

                    if (texture.isReadable)
                    {
                        var pixels = texture.GetPixels();
                        Color[] occlusionPixels = null;
                        if (occlusionTexture != null &&
                            occlusionTexture.isReadable)
                        {
                            occlusionPixels = occlusionTexture.GetPixels();
                        }

                        var pixelCache = new Color[pixels.Length];

                        for (int c = 0; c < pixels.Length; c++)
                        {
                            pixelCache[c].r = pixels[c].b; // MRTK standard shader metallic value, glTF metallic value
                            pixelCache[c].g = occlusionPixels?[c].r ?? 1.0f; // MRTK standard shader occlusion value, glTF occlusion value if available
                            pixelCache[c].b = 0f; // MRTK standard shader emission value
                            pixelCache[c].a = (1.0f - pixels[c].g); // MRTK standard shader smoothness value, invert of glTF roughness value
                        }

                        texture.SetPixels(pixelCache);
                        texture.Apply();

                        material.SetTexture(channelMapPropId, texture);
                        material.EnableKeyword("_CHANNEL_MAP");
                    }
                    else
                    {
                        material.DisableKeyword("_CHANNEL_MAP");
                    }

                    material.SetFloat(glossinessPropId, Mathf.Abs((float)gltfMaterial.pbrMetallicRoughness.roughnessFactor - 1f));
                    material.SetFloat(metallicPropId, (float)gltfMaterial.pbrMetallicRoughness.metallicFactor);
                }
            }

            // Apply alpha mode settings to material
            ConfigureMrtkAlphaMode(material, gltfMaterial.alphaModeEnum);

            // Apply emissiveTexture if available
            if (gltfMaterial.emissiveTexture?.index >= 0 && material.HasProperty(emissionMapPropId))
            {
                //material.EnableKeyword(KW_EMISSION_MAP);
                material.EnableKeyword(KW_EMISSION);
                //material.SetTexture(emissionMapPropId, gltf.GetImage(gltfMaterial.emissiveTexture.index));
                material.SetColor(emissionColorPropId, gltfMaterial.emissive);
            }

            if (gltfMaterial.normalTexture?.index >= 0)
            {
                material.SetTexture(normalMapPropId, gltf.GetImage(gltfMaterial.normalTexture.index));
                material.SetFloat(normalMapScalePropId, gltfMaterial.normalTexture.scale);
                material.EnableKeyword(KW_NORMAL_MAP);
            }

            if (gltfMaterial.doubleSided)
            {
                material.SetFloat(cullModePropId, (float)UnityEngine.Rendering.CullMode.Off);
            }

            material.color = baseColorLinear.gamma;
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            Debug.Log("Returning material: " + material.name);
            return material;
        }

        public static void ConfigureMrtkAlphaMode(Material material, AlphaMode mode)
        {
            switch (mode)
            {
                case AlphaMode.OPAQUE:
                    // No necessary changes
                    break;
                case AlphaMode.MASK:
                    material.SetInt(srcBlendPropId, (int)UnityEngine.Rendering.BlendMode.One);
                    material.SetInt(dstBlendPropId, (int)UnityEngine.Rendering.BlendMode.Zero);
                    material.SetInt(zWritePropId, 1);
                    material.SetInt(modePropId, 3);
                    material.SetOverrideTag(TAG_RENDER_TYPE, TAG_RENDER_TYPE_MRTK_CUTOUT);
                    material.EnableKeyword(KW_ALPHATEST_ON);
                    material.DisableKeyword(KW_ALPHABLEND_ON);
                    material.DisableKeyword(KW_ALPHAPREMULTIPLY_ON);
                    material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest; //2450
                    return;
                case AlphaMode.BLEND:
                    material.SetInt(srcBlendPropId, (int)UnityEngine.Rendering.BlendMode.One);
                    material.SetInt(dstBlendPropId, (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    material.SetInt(zWritePropId, 0);
                    material.SetInt(modePropId, 3);
                    material.SetOverrideTag(TAG_RENDER_TYPE, TAG_RENDER_TYPE_MRTK_TRANSPARENCY);
                    material.DisableKeyword(KW_ALPHATEST_ON);
                    material.DisableKeyword(KW_ALPHABLEND_ON);
                    material.EnableKeyword(KW_ALPHAPREMULTIPLY_ON);
                    material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent; //3000
                    return;
            }
        }


        /*
        /// <summary>
        /// Configures material for alpha masking.
        /// </summary>
        /// <param name="material">Target material</param>
        /// <param name="alphaCutoff">Threshold value for alpha masking</param>
        public static void SetAlphaModeMask(UnityEngine.Material material, float alphaCutoff)
        {
            material.EnableKeyword(KW_ALPHATEST_ON);
            material.SetInt(zWritePropId, 1);
            material.DisableKeyword(KW_ALPHAPREMULTIPLY_ON);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;  //2450
            material.SetFloat(cutoffPropId, alphaCutoff);
            material.SetFloat(modePropId, (int)StandardShaderMode.Cutout);
            material.SetOverrideTag(TAG_RENDER_TYPE, TAG_RENDER_TYPE_CUTOUT);
            material.SetInt(srcBlendPropId, (int)UnityEngine.Rendering.BlendMode.One);
            material.SetInt(dstBlendPropId, (int)UnityEngine.Rendering.BlendMode.Zero);
            material.DisableKeyword(KW_ALPHABLEND_ON);
        }

        /// <summary>
        /// Configures material for alpha masking.
        /// </summary>
        /// <param name="material">Target material</param>
        /// <param name="gltfMaterial">Source material</param>
        public static void SetAlphaModeMask(UnityEngine.Material material, Schema.Material gltfMaterial)
        {
            SetAlphaModeMask(material, gltfMaterial.alphaCutoff);
        }

        /// <summary>
        /// Configures material for alpha blending.
        /// </summary>
        /// <param name="material">Target material</param>
        public static void SetAlphaModeBlend( UnityEngine.Material material ) {
            material.SetFloat(modePropId, (int)StandardShaderMode.Fade);
            material.SetOverrideTag(TAG_RENDER_TYPE, TAG_RENDER_TYPE_FADE);
            material.EnableKeyword(KW_ALPHABLEND_ON);
            material.SetInt(srcBlendPropId, (int)UnityEngine.Rendering.BlendMode.SrcAlpha);//5
            material.SetInt(dstBlendPropId, (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);//10
            material.SetInt(zWritePropId, 0);
            material.DisableKeyword(KW_ALPHAPREMULTIPLY_ON);
            material.DisableKeyword(KW_ALPHATEST_ON);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;  //3000
        }

        /// <summary>
        /// Configures material for transparency.
        /// </summary>
        /// <param name="material">Target material</param>
        public static void SetAlphaModeTransparent( UnityEngine.Material material ) {
            material.SetFloat(modePropId, (int)StandardShaderMode.Fade);
            material.SetOverrideTag(TAG_RENDER_TYPE, TAG_RENDER_TYPE_TRANSPARENT);
            material.EnableKeyword(KW_ALPHAPREMULTIPLY_ON);
            material.SetInt(srcBlendPropId, (int)UnityEngine.Rendering.BlendMode.One);//1
            material.SetInt(dstBlendPropId, (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);//10
            material.SetInt(zWritePropId, 0);
            material.DisableKeyword(KW_ALPHABLEND_ON);
            material.DisableKeyword(KW_ALPHATEST_ON);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;  //3000
        }

        /// <summary>
        /// Configures material to be opaque.
        /// </summary>
        /// <param name="material">Target material</param>
        public static void SetOpaqueMode(UnityEngine.Material material) {
            material.SetOverrideTag(TAG_RENDER_TYPE, TAG_RENDER_TYPE_OPAQUE);
            material.DisableKeyword(KW_ALPHABLEND_ON);
            material.renderQueue = -1;
            material.SetInt(srcBlendPropId, (int)UnityEngine.Rendering.BlendMode.One);
            material.SetInt(dstBlendPropId, (int)UnityEngine.Rendering.BlendMode.Zero);
            material.SetInt(zWritePropId, 1);
            material.DisableKeyword(KW_ALPHATEST_ON);
            material.DisableKeyword(KW_ALPHAPREMULTIPLY_ON);
        }*/
    }
}
#endif // GLTFAST_BUILTIN_RP || UNITY_EDITOR
