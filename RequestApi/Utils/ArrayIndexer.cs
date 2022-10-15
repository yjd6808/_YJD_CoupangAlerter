/* * * * * * * * * * * * * 
 * 작성자: 윤정도
 * * * * * * * * * * * * *
 * 값 타입 배열에 안전한 읽기/쓰기 기능 추가
 * * * * * * * * * * * * * * 
 */

using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace RequestApi.Utils
{
    public class ArrayIndexer<T> where T : struct
    {
        protected T[] _array;

        public virtual T this[int idx]
        {
            get
            {
                if (idx < 0 || idx >= _array.Length) throw new IndexOutOfRangeException();
                return _array[idx];
            }
            set
            {
                if (idx < 0 || idx >= _array.Length) throw new IndexOutOfRangeException();
                _array[idx] = value;
            }
        }

        public ArrayIndexer(int size)
        {
            _array = new T[size];
        }

        public virtual void Apply(Action<T[]> applyAction) => applyAction(_array);
    }

    public class ThreadSafeArrayIndexer<T> : ArrayIndexer<T> where T : struct
    {
        public override T this[int idx]
        {
            get
            {
                lock (this)
                {
                    if (idx < 0 || idx >= _array.Length) throw new IndexOutOfRangeException();
                    return _array[idx];
                }
            }
            set
            {
                lock (this)
                {
                    if (idx < 0 || idx >= _array.Length) throw new IndexOutOfRangeException();
                    _array[idx] = value;
                }
            }
        }

        public ThreadSafeArrayIndexer(int size) : base(size)
        {
        }

        public override void Apply(Action<T[]> applyAction)
        {
            lock (this)
                applyAction(_array);
        }
    }
}
