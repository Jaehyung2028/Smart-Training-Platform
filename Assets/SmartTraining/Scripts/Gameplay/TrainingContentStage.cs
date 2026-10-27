using UnityEngine;

namespace SmartTraining
{
    // 콘텐츠별 환경을 만들기 전 사용하는 공통 진입 씬입니다. 서버의 이동 허용 범위와 바닥 크기를 맞춥니다.
    public sealed class TrainingContentStage : MonoBehaviour
    {
        private Material floorMaterial;

        private GameObject floor;

        private void Awake()
        {
            CreateFloor();
            CreateLight();
        }

        private void CreateFloor()
        {
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Training Floor";
            floor.transform.SetParent(transform, false);
            floor.transform.localPosition = new Vector3(0, -0.1f, 0);
            floor.transform.localScale = new Vector3(26, 0.2f, 26);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader != null)
            {
                floorMaterial = new Material(shader)
                {
                    color = new Color32(100, 116, 139, 255)
                };
                floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;
            }
        }

        private void CreateLight()
        {
            var lightObject = new GameObject("Training Light", typeof(Light));
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.rotation = Quaternion.Euler(50, -30, 0);
            var light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
        }

        private void OnDestroy()
        {
            if (floorMaterial != null)
            {
                Destroy(floorMaterial);
            }
        }
    }
}
