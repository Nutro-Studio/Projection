using System.Collections.Generic;
using Core.Board;
using UnityEngine;

namespace Presentation.Map
{
    public sealed class MapEdgeRenderer : MonoBehaviour
    {
        private static readonly Color EdgeColor = new(0.45f, 0.52f, 0.58f, 0.7f);
        private const float LineWidth = 0.08f;

        private readonly List<GameObject> _lines = new();

        public void BuildEdges(IReadOnlyList<ProvinceState> provinces, IReadOnlyList<ProvinceView> views)
        {
            Clear();
            var material = new Material(Shader.Find("Sprites/Default"));

            for (int i = 0; i < provinces.Count; i++)
            {
                var province = provinces[i];
                Vector3 start = views[province.Id].transform.position;
                start.z = 0.1f;

                for (int n = 0; n < province.Neighbours.Count; n++)
                {
                    int neighbourId = province.Neighbours[n];
                    if (province.Id >= neighbourId || neighbourId < 0 || neighbourId >= views.Count) continue;

                    Vector3 end = views[neighbourId].transform.position;
                    end.z = 0.1f;

                    var edgeObj = new GameObject($"Edge_{province.Id}_{neighbourId}");
                    edgeObj.transform.SetParent(transform, false);

                    var lr = edgeObj.AddComponent<LineRenderer>();
                    lr.material = material;
                    lr.startColor = EdgeColor;
                    lr.endColor = EdgeColor;
                    lr.startWidth = LineWidth;
                    lr.endWidth = LineWidth;
                    lr.positionCount = 2;
                    lr.useWorldSpace = true;
                    lr.sortingOrder = -1;
                    lr.SetPosition(0, start);
                    lr.SetPosition(1, end);

                    _lines.Add(edgeObj);
                }
            }
        }

        private void Clear()
        {
            for (int i = 0; i < _lines.Count; i++)
            {
                if (_lines[i] != null) Destroy(_lines[i]);
            }

            _lines.Clear();
        }
    }
}