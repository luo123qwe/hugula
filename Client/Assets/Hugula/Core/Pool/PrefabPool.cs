﻿// Copyright (c) 2015 hugula
// direct https://github.com/tenvick/hugula
//
using UnityEngine;
using System.Collections.Generic;
using Hugula.Utils;

namespace Hugula.Pool
{
    /// <summary>
    /// prefab 缓存池
    /// </summary>
    [SLua.CustomLuaClass]
    public class PrefabPool : MonoBehaviour
    {
        #region const config

        public const float deltaTime30 = 0.033f;
        public const float deltaTime25 = 0.04f;
        public const float deltaTime20 = 0.05f;
        public const float deltaTime15 = 0.067f;

        #endregion

        #region instance
        private static int removeCount = 1;
        private static float lastGcTime = 0;//上传检测GC时间
        private static float gcDeltaTime = 0;//上传检测GC时间
        //标记回收
        private static Dictionary<int, float> removeMark = new Dictionary<int, float>();

        void Awake()
        {
            DontDestroyOnLoad(this.gameObject);
        }

        /// <summary>
        /// 内存阈值检测和自动对象回收
        /// </summary>
        void Update() //每帧删除
        {
            if (willGcList.Count == 0) //如果正在gc不需要判断
            {
                gcDeltaTime = Time.unscaledTime - lastGcTime;
                if (gcDeltaTime >= gcDeltaTimeConfig)//20s检测一次
                {
                    float totalMemory = HugulaProfiler.GetTotalAllocatedMemory();
                    //Debug.Log(totalMemory);
                    if (totalMemory > threshold3)
                    {
                        AutoGC(gcSegment3);
                    }
                    else if (totalMemory > threshold2)
                    {
                        AutoGC(gcSegment2);
                    }
                    else if (totalMemory > threshold1)
                    {
                        AutoGC(gcSegment1);
                    }

                    lastGcTime = Time.unscaledTime;
                }// 
            }


            if (willGcList.Count > 0) //如果有需要回收的gameobject
            {
                var fps = Time.time / (float)Time.frameCount;
                if (fps >= deltaTime30)
                {
                    removeCount = 4;
                }
                else if (fps >= deltaTime25)
                {
                    removeCount = 3;
                }
                else if (fps >= deltaTime20)
                {
                    removeCount = 2;
                }
                else //if (fps >= deltaTime15)
                {
                    removeCount = 1;
                }

                DisposeRefer(removeCount);//移除数量
            }

        }

        /// <summary>
        /// 真正销毁对象
        /// </summary>
        /// <param name="count"></param>
        void DisposeRefer(int count)
        {
            if (count > willGcList.Count) count = willGcList.Count;
            int referKey;//要删除的项目
            var begin=System.DateTime.Now;
            while (count > 0)
            {
                referKey = willGcList.Dequeue();
                if (removeMark.ContainsKey(referKey))
                {
                    ClearKey(referKey);
                    removeMark.Remove(referKey);
                }
                var ts = System.DateTime.Now - begin;
                if(ts.TotalSeconds > deltaTime25) break;
                // Debug.Log("Dispose " + referRemove.name);
                count--;
            }

        }

        void OnDestroy()
        {
            if (_gameObject == this.gameObject)
                _gameObject = null;
            ClearAllCache();
            Clear();
        }


        #endregion

        #region static


        #region config
        /// <summary>
        /// 两次GC检测间隔时间
        /// </summary>
        public static float gcDeltaTimeConfig = 10f;//两次GC检测时间S

#if UNITY_EDITOR
        /// <summary>
        /// 内存阈值
        /// </summary>
        public static float threshold1 = 150f;
        public static float threshold2 = 190f;
        public static float threshold3 = 250f;
#else
    /// <summary>
    /// 内存阈值
    /// </summary>
    public static float threshold1 = 50f;
    public static float threshold2 = 100f;
    public static float threshold3 = 150f;
#endif

        /// <summary>
        /// 回收片段
        /// </summary>
        private const byte gcSegment1 = 1;//片段索引
        private const byte gcSegment2 = 3;
        private const byte gcSegment3 = 6;
        #endregion

        #region private member
        /// <summary>
        /// 总共分0-8
        /// </summary>
        private const byte SegmentSize = 8;

        /// <summary>
        /// 回收列表
        /// </summary>
        private static Queue<int> willGcList = new Queue<int>();

        /// <summary>
        /// 原始缓存
        /// </summary>
        private static Dictionary<int, GameObject> originalPrefabs = new Dictionary<int, GameObject>();

        /// <summary>
        /// 原始资源标记缓存
        /// </summary>
        private static Dictionary<GameObject, bool> assetsFlag = new Dictionary<GameObject, bool>();

        /// <summary>
        /// 类型用于做回收策略
        /// </summary>
        private static Dictionary<int, byte> prefabsType = new Dictionary<int, byte>();

        /// <summary>
        /// 被引用
        /// </summary>
        private static Dictionary<int, HashSet<ReferGameObjects>> prefabRefCaches = new Dictionary<int, HashSet<ReferGameObjects>>();

        /// <summary>
        /// 可用队列
        /// </summary>
        private static Dictionary<int, Queue<ReferGameObjects>> prefabFreeQueue = new Dictionary<int, Queue<ReferGameObjects>>();

        #endregion

        #region pulic method
        /// <summary>
        /// 清除所有缓存
        /// </summary>
        public static void Clear()
        {
            var values = originalPrefabs.Values.GetEnumerator();
            //foreach (Object item in values)
            while (values.MoveNext())
            {
                var item = values.Current;
                DestroyOriginalPrefabs(item);
            }

            originalPrefabs.Clear();
        }

        /// <summary>
        /// 添加原始缓存
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public static bool Add(string key, GameObject value, byte type)
        {
            int hashkey = LuaHelper.StringToHash(key);
            return Add(hashkey, value, type);
        }

        /// <summary>
        /// 添加原始缓存
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public static bool Add(string key, GameObject value, byte type, bool isAsset)
        {
            int hashkey = LuaHelper.StringToHash(key);
            return Add(hashkey, value, type, isAsset);
        }

        internal static void DestroyOriginalPrefabs(GameObject obj)
        {
            if (assetsFlag.ContainsKey(obj))
            {
                assetsFlag.Remove(obj);
            }
            else
            {
                GameObject.Destroy(obj);
            }
        }

        private static void AddRemoveMark(int hash)
        {
            removeMark[hash] = Time.unscaledTime + 0.5f;
        }

        private static void DeleteRemveMark(int hash)
        {
            removeMark.Remove(hash);
        }

        /// <summary>
        /// 添加原始项目
        /// </summary>
        /// <param name="hash"></param>
        /// <param name="value"></param>
        internal static bool Add(int hash, GameObject value, byte type, bool isAsset = false)
        {
            bool contains = originalPrefabs.ContainsKey(hash);
            originalPrefabs[hash] = value;
            if (isAsset) assetsFlag[value] = isAsset;
            if (!contains) //不能重复添加
            {
                if (type > SegmentSize) type = SegmentSize;
                prefabFreeQueue[hash] = new Queue<ReferGameObjects>(); //空闲队列
                prefabsType[hash] = type;
                prefabRefCaches[hash] = new HashSet<ReferGameObjects>();//引用列表

#if UNITY_EDITOR
                LuaHelper.RefreshShader(value);
#endif
                return true;
            }
            return false;
        }

        /// <summary>
        /// 获取值
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static GameObject Get(string key)
        {
            int hash = LuaHelper.StringToHash(key);
            GameObject re = Get(hash);
            return re;
        }

        internal static GameObject Get(int hash)
        {
            GameObject obj = null;
            originalPrefabs.TryGetValue(hash, out obj);
            return obj;
        }

        /// <summary>
        /// 是否包含cacheKey
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static bool ContainsKey(string key)
        {
            int hash = LuaHelper.StringToHash(key);
            return ContainsKey(hash);
        }

        internal static bool ContainsKey(int hash)
        {
            return originalPrefabs.ContainsKey(hash);
        }

        /// <summary>
        /// 获取值
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static ReferGameObjects GetCache(string key)
        {
            int hash = LuaHelper.StringToHash(key);
            return GetCache(hash);
        }

        /// <summary>
        /// 获取可用的实例
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        internal static ReferGameObjects GetCache(int key)
        {
            ReferGameObjects refer = null;
            //空闲队列
            Queue<ReferGameObjects> prefabQueueMe = null;
            if (!prefabFreeQueue.TryGetValue(key, out prefabQueueMe)) return null;

            //引用列表
            HashSet<ReferGameObjects> prefabCachesMe = null;
            if (!prefabRefCaches.TryGetValue(key, out prefabCachesMe)) return null;

            //移除回收引用
            DeleteRemveMark(key);

            if (prefabQueueMe.Count > 0)
            {
                refer = prefabQueueMe.Dequeue();//出列
                //保持引用
                prefabCachesMe.Add(refer);
                return refer;
            }
            else
            {
                GameObject prefab = Get(key);
                if (prefab == null) //如果没有 返回NUll
                    return null;

                GameObject clone = (GameObject)GameObject.Instantiate(prefab);
                var comp = clone.GetComponent<ReferGameObjects>();
                if (comp == null)
                {
                    comp = clone.AddComponent<ReferGameObjects>();
                }
                refer = comp;
                refer.cacheHash = key;
                //保持引用
                prefabCachesMe.Add(refer); //放入引用列表
            }

            return refer;
        }

        /// <summary>
        /// 放入缓存
        /// </summary>
        /// <param name="cop"></param>
        public static bool StoreCache(ReferGameObjects refer)
        {
            HashSet<ReferGameObjects> prefabCachesMe = null;
            int keyHash = refer.cacheHash;
            if (prefabRefCaches.TryGetValue(keyHash, out prefabCachesMe))
            {//从引用列表寻找
                bool isremove = prefabCachesMe.Remove(refer);
                if (isremove)
                {
                    Queue<ReferGameObjects> prefabQueueMe = null;
                    if (prefabFreeQueue.TryGetValue(keyHash, out prefabQueueMe))
                    {
                        prefabQueueMe.Enqueue(refer);//入列
                        
                        if (prefabCachesMe.Count == 0 && prefabsType[keyHash] < SegmentSize) AddRemoveMark(keyHash);//如果引用为0标记回收
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 放入缓存
        /// </summary>
        /// <param name="gobj"></param>
        /// <returns></returns>
        public static bool StoreCache(GameObject gobj)
        {
            ReferGameObjects refer = gobj.GetComponent<ReferGameObjects>();
            if (refer != null && refer.cacheHash != 0)
                return StoreCache(refer);
            return false;
        }

        /// <summary>
        /// Clears all cache.
        /// </summary>
        public static void ClearAllCache()
        {
            prefabsType.Clear();
            willGcList.Clear();
            removeMark.Clear();
            var freeValues = prefabFreeQueue.Values.GetEnumerator();
            while (freeValues.MoveNext())
            {
                var queue = freeValues.Current;
                while (queue.Count > 0)
                {
                    var refer = queue.Dequeue();
                    if (refer) GameObject.Destroy(refer.gameObject);
                }
            }
            prefabFreeQueue.Clear();

            var refValues = prefabRefCaches.Values.GetEnumerator();

            ReferGameObjects item;
            while (refValues.MoveNext())
            {
                var items = refValues.Current.GetEnumerator();
                while (items.MoveNext())
                {
                    item = items.Current;
                    if (item) GameObject.Destroy(item.gameObject);
                }
                items.Dispose();
            }
            prefabRefCaches.Clear();
        }

        // public static void Remove(string key)
        // {
        //     int hash = LuaHelper.StringToHash(key);
        //     Remove(hash);
        // }

        internal static void Remove(int key)
        {
            GameObject obj = null;
            if (originalPrefabs.TryGetValue(key, out obj))
            {
#if UNITY_EDITOR
                // Debug.LogFormat("Remove={0},hash={1}",obj.name,key);
#endif
                DestroyOriginalPrefabs(obj);
            }

            originalPrefabs.Remove(key);
            prefabsType.Remove(key);
            prefabFreeQueue.Remove(key);
            prefabRefCaches.Remove(key);
        }

        /// <summary>
        /// 标记删除 如果有引用不会被删除
        /// </summary>
        public static int MarkRemove(string key)
        {
            int hash = LuaHelper.StringToHash(key);
            int referCount = 0;
            HashSet<ReferGameObjects> refers = null;
            if (prefabRefCaches.TryGetValue(hash, out refers))
            {
                referCount = refers.Count;
            }
            AddRemoveMark(hash);
            willGcList.Enqueue(hash);
            return referCount;
        }

        /// <summary>
        /// 强行回收
        /// </summary>
        /// <param name="key"></param>
        /// <param name="force"></param>
        /// <returns></returns>
        public static bool ClearCacheImmediate(string key)
        {
            int hash = LuaHelper.StringToHash(key);
            // 
            HashSet<ReferGameObjects> refers = null;

            if (prefabRefCaches.TryGetValue(hash, out refers))
            {
                Queue<ReferGameObjects> freequeue;
                var referItem = refers.GetEnumerator();
                if (prefabFreeQueue.TryGetValue(hash, out freequeue))
                {
                    while (referItem.MoveNext())
                    {
                        freequeue.Enqueue(referItem.Current);
                    }
                }
                refers.Clear();
            }
            ClearKey(hash);
            return true;
        }

        /// <summary>
        /// 清理当前key的对象
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        internal static void ClearKey(int key)
        {
            HashSet<ReferGameObjects> refers = null;
            if (prefabRefCaches.TryGetValue(key, out refers) && refers.Count == 0)
            {
                Queue<ReferGameObjects> freequeue;
                ReferGameObjects refer = null;
                if (prefabFreeQueue.TryGetValue(key, out freequeue))
                {
                    while (freequeue.Count > 0)
                    {
                        refer = freequeue.Dequeue();
                        if (refer) GameObject.Destroy(refer.gameObject);
                    }
                }

                Remove(key); //移除原始项目
            }
#if UNITY_EDITOR
            else
                Debug.LogFormat("<color=yellow>the object(name={1},hash={0}) has refers</color>", key, Get(key) != null ? Get(key).name : "null");
#endif


        }

        /// <summary>
        /// 自动回收对象当前segmentindex和之前的所有片段
        /// </summary>
        internal static void AutoGC(byte segmentIndex,bool compareTime = true)
        {
            var items = removeMark.GetEnumerator();
            int key = 0;
            byte keyType = 0;
            while (items.MoveNext())
            {
                var kv = items.Current;
                key = kv.Key;
                keyType = 0;
                prefabsType.TryGetValue(key, out keyType);
                if (keyType <= segmentIndex && compareTime?Time.unscaledTime >= kv.Value:true)
                {
                    willGcList.Enqueue(key);
                }
            }
        }


        /// <summary>
        /// 手动回收 可以释放的对象
        /// </summary>
        /// <param name="segmentIndex"></param>
        /// <returns></returns>
        public static void GCCollect(byte segmentIndex)
        {
            AutoGC(segmentIndex,false);
        }

        #endregion

        #endregion

        #region gameobject
        private static GameObject _gameObject;

        public static GameObject instance
        {
            get
            {
                if (_gameObject == null)
                {
                    _gameObject = new GameObject("PrefabPool");
                    _gameObject.AddComponent<PrefabPool>();
                }

                return _gameObject;
            }
        }
        #endregion
    }


    public static class HugulaProfiler
    {
        const float m_KBSize = 1024.0f * 1024.0f;

        public static float GetTotalAllocatedMemory()
        {
            float totalMemory = (float)(Profiler.GetTotalAllocatedMemory() / m_KBSize);
            return totalMemory;
        }
    }
}