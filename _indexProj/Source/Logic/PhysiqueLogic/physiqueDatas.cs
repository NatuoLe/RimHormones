using System.Collections.Generic;
using Verse;

namespace Hormones.Logic.PhysiqueLogic
{
    public static class PhysiqueDatas
    {
        private static readonly Queue<object> vector3Pool = new Queue<object>();
        private static readonly int initialPoolSize = 10;
        private static bool initialized = false;
        private static System.Type vector3Type;

        public static void Initialize()
        {
            if (initialized) return;

            vector3Type = System.Type.GetType("UnityEngine.Vector3, UnityEngine.CoreModule");
            if (vector3Type != null)
            {
                for (int i = 0; i < initialPoolSize; i++)
                {
                    vector3Pool.Enqueue(System.Activator.CreateInstance(vector3Type, 0f, 0f, 0f));
                }
            }

            initialized = true;
        }

        public static object GetVector3(float x, float y, float z)
        {
            if (!initialized || vector3Type == null)
            {
                return System.Activator.CreateInstance(vector3Type ?? System.Type.GetType("UnityEngine.Vector3, UnityEngine.CoreModule"), x, y, z);
            }

            object vector3;
            if (vector3Pool.Count > 0)
            {
                vector3 = vector3Pool.Dequeue();
            }
            else
            {
                vector3 = System.Activator.CreateInstance(vector3Type, 0f, 0f, 0f);
            }

            System.Reflection.FieldInfo xField = vector3Type.GetField("x");
            System.Reflection.FieldInfo yField = vector3Type.GetField("y");
            System.Reflection.FieldInfo zField = vector3Type.GetField("z");

            if (xField != null) xField.SetValue(vector3, x);
            if (yField != null) yField.SetValue(vector3, y);
            if (zField != null) zField.SetValue(vector3, z);

            return vector3;
        }

        public static void ReturnVector3(object vector3)
        {
            if (!initialized || vector3 == null) return;
            vector3Pool.Enqueue(vector3);
        }
    }
}