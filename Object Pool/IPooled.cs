using System.ComponentModel;
using UnityEngine.Scripting;

namespace VolumeBox.Toolbox
{
    public interface IPooled<T>: IPooledBase
    {
        void OnSpawn(T data);

        [EditorBrowsable(EditorBrowsableState.Never), Preserve]
        internal void InvokePooledSpawn(object data)
        {
            OnSpawn((T)data);
        }
    }

    public interface IPooled: IPooledBase
    {
        void OnSpawn();

        [EditorBrowsable(EditorBrowsableState.Never), Preserve]
        internal void InvokePooledSpawn(object data)
        {
            OnSpawn();
        }
    }

    public interface IPooledBase
    {
        
    }
}
