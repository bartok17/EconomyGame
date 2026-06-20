using UnityEngine;

namespace MonopolyGame.Board
{
    public sealed class BoardLayoutBuilder : MonoBehaviour
    {
        [SerializeField] private int edgeSpacesPerSide = 9;
        [SerializeField] private float cornerSize = 2f;
        [SerializeField] private float edgeLength = 1.3f;
        [SerializeField] private float laneDepth = 1.8f;
        [SerializeField] private float tileHeight = 0.12f;

        [ContextMenu("Rebuild Board")]
        public void RebuildBoard()
        {
            ClearChildren();

            var spacesRoot = new GameObject("Spaces").transform;
            spacesRoot.SetParent(transform, false);

            var decorRoot = new GameObject("Decor").transform;
            decorRoot.SetParent(transform, false);

            float cornerOffset = edgeSpacesPerSide * edgeLength / 2f + cornerSize / 2f;
            float outerSize = cornerOffset * 2f + cornerSize;
            float innerSize = outerSize - laneDepth * 2f;

            MakeCube("BoardBase", decorRoot, new Vector3(0, -0.04f, 0), new Vector3(outerSize, 0.06f, outerSize), new Color(0.18f, 0.20f, 0.20f));
            MakeCube("InnerArea", decorRoot, new Vector3(0, 0.02f, 0), new Vector3(innerSize, 0.04f, innerSize), new Color(0.62f, 0.65f, 0.63f));
            MakeCube("PlayerZone", decorRoot, new Vector3(0, 0.03f, -cornerOffset - 3f), new Vector3(outerSize * 0.9f, 0.06f, 2.1f), new Color(0.72f, 0.20f, 0.65f));
            
            MakeCube("ActionDeck", decorRoot, new Vector3(-3.6f, 0.22f, 3.6f), new Vector3(2.2f, 0.28f, 1.1f), new Color(0.75f, 0.02f, 0.10f), 135f);
            MakeCube("EventDeck", decorRoot, new Vector3(3.6f, 0.22f, -3.6f), new Vector3(2.2f, 0.28f, 1.1f), new Color(0.02f, 0.45f, 0.65f), 135f);

            int index = 0;
            
            MakeSpace(spacesRoot, index++, "Start", BoardSpaceType.Start, new Vector3(cornerOffset, 0, -cornerOffset), new Vector3(cornerSize, tileHeight, cornerSize));
            
            for (int i = 0; i < edgeSpacesPerSide; i++)
            {
                float x = cornerOffset - cornerSize / 2f - edgeLength / 2f - (i * edgeLength);
                MakeAutoSpace(spacesRoot, index++, new Vector3(x, 0, -cornerOffset), new Vector3(edgeLength * 0.92f, tileHeight, laneDepth));
            }
            
            MakeSpace(spacesRoot, index++, "Jail", BoardSpaceType.Jail, new Vector3(-cornerOffset, 0, -cornerOffset), new Vector3(cornerSize, tileHeight, cornerSize));
            
            for (int i = 0; i < edgeSpacesPerSide; i++)
            {
                float z = -cornerOffset + cornerSize / 2f + edgeLength / 2f + (i * edgeLength);
                MakeAutoSpace(spacesRoot, index++, new Vector3(-cornerOffset, 0, z), new Vector3(laneDepth, tileHeight, edgeLength * 0.92f));
            }
            
            MakeSpace(spacesRoot, index++, "Parking", BoardSpaceType.Parking, new Vector3(-cornerOffset, 0, cornerOffset), new Vector3(cornerSize, tileHeight, cornerSize));
            
            for (int i = 0; i < edgeSpacesPerSide; i++)
            {
                float x = -cornerOffset + cornerSize / 2f + edgeLength / 2f + (i * edgeLength);
                MakeAutoSpace(spacesRoot, index++, new Vector3(x, 0, cornerOffset), new Vector3(edgeLength * 0.92f, tileHeight, laneDepth));
            }
            
            MakeSpace(spacesRoot, index++, "Go To Jail", BoardSpaceType.GoToJail, new Vector3(cornerOffset, 0, cornerOffset), new Vector3(cornerSize, tileHeight, cornerSize));
            
            for (int i = 0; i < edgeSpacesPerSide; i++)
            {
                float z = cornerOffset - cornerSize / 2f - edgeLength / 2f - (i * edgeLength);
                MakeAutoSpace(spacesRoot, index++, new Vector3(cornerOffset, 0, z), new Vector3(laneDepth, tileHeight, edgeLength * 0.92f));
            }
            
        }

        private void MakeAutoSpace(Transform parent, int index, Vector3 position, Vector3 scale)
        {
            BoardSpaceType type = GetSpaceType(index);
            MakeSpace(parent, index, type.ToString(), type, position, scale);
        }
        
        private static BoardSpaceType GetSpaceType(int index)
        {
            return index switch
            {
                0 => BoardSpaceType.Start,
                10 => BoardSpaceType.Jail,
                20 => BoardSpaceType.Parking,
                30 => BoardSpaceType.GoToJail,
                4 or 38 => BoardSpaceType.Tax,
                5 or 7 or 22 or 25 or 35 or 36 => BoardSpaceType.ActionCard,
                2 or 12 or 15 or 17 or 28 or 33 => BoardSpaceType.EventCard,
                _ => BoardSpaceType.Property
            };
        }

        private void MakeSpace(Transform parent, int index, string label, BoardSpaceType type, Vector3 position, Vector3 scale)
        {
            var go = MakeCube($"Space_{index:00}_{label}", parent, position, scale, ColorFor(type));
            var view = go.AddComponent<BoardSpaceView>();
            view.index = index;
            view.displayName = label;
            view.type = type;
            
            AssignSpaceData(view);
        }

        private void AssignSpaceData(BoardSpaceView view)
        {
            if (view.type == BoardSpaceType.Tax)
            {
                view.price = (view.index == 4) ? 200 : 100;
            }
            else if (view.type == BoardSpaceType.Property)
            {
                switch (view.index)
                {
                    case 1:
                        view.price = 60;
                        view.baseRent = 2;
                        view.houseCost = 50;
                        break;
                    case 3:
                        view.price = 60;
                        view.baseRent = 4;
                        view.houseCost = 50;
                        break;

                    case 6:
                        view.price = 100;
                        view.baseRent = 6;
                        view.houseCost = 50;
                        break;
                    case 8:
                        view.price = 100;
                        view.baseRent = 6;
                        view.houseCost = 50;
                        break;
                    case 9:
                        view.price = 120;
                        view.baseRent = 8;
                        view.houseCost = 50;
                        break;

                    case 11:
                        view.price = 140;
                        view.baseRent = 10;
                        view.houseCost = 100;
                        break;
                    case 13:
                        view.price = 140;
                        view.baseRent = 10;
                        view.houseCost = 100;
                        break;
                    case 14:
                        view.price = 160;
                        view.baseRent = 12;
                        view.houseCost = 100;
                        break;

                    case 16:
                        view.price = 200;
                        view.baseRent = 25;
                        view.houseCost = 0;
                        break;
                    case 18:
                        view.price = 200;
                        view.baseRent = 25;
                        view.houseCost = 0;
                        break;
                    case 19:
                        view.price = 200;
                        view.baseRent = 25;
                        view.houseCost = 0;
                        break;

                    default:
                        view.price = 200;
                        view.baseRent = 15;
                        view.houseCost = 100;
                        break;
                }
            }
        }

        private GameObject MakeCube(string name, Transform parent, Vector3 position, Vector3 scale, Color color, float yRotation = 0f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0, yRotation, 0);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = NewMaterial(color);
            return go;
        }

        private static Color ColorFor(BoardSpaceType type)
        {
            return type switch
            {
                BoardSpaceType.Start => new Color(0.80f, 0.80f, 0.78f),
                BoardSpaceType.Property => new Color(0.34f, 0.36f, 0.36f),
                BoardSpaceType.ActionCard => new Color(0.55f, 0.12f, 0.18f),
                BoardSpaceType.EventCard => new Color(0.10f, 0.36f, 0.52f),
                BoardSpaceType.Tax => new Color(0.55f, 0.42f, 0.18f),
                BoardSpaceType.Jail => new Color(0.45f, 0.45f, 0.48f),
                BoardSpaceType.Parking => new Color(0.40f, 0.48f, 0.42f),
                BoardSpaceType.GoToJail => new Color(0.42f, 0.32f, 0.48f),
                _ => new Color(0.50f, 0.50f, 0.50f)
            };
        }

        private static Material NewMaterial(Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = color;
            return material;
        }

        private void ClearChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }
        }
    }
}