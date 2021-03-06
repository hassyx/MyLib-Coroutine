﻿using System.Collections;
using System.Collections.Generic;

public class Main : UnityEngine.MonoBehaviour
{
	private static Dictionary<MyLib.MonoBehaviour, MyLib.BehaviourData> behaviourDict = 
		new Dictionary<MyLib.MonoBehaviour, MyLib.BehaviourData>();

	public static void AddMonoBehaviour(MyLib.MonoBehaviour behaviour)
	{
		if (!behaviourDict.ContainsKey(behaviour))
		{
			behaviourDict.Add(behaviour, new MyLib.BehaviourData(behaviour));
		}
	}

	/// <summary>
	/// このメソッドの実行中に StartCoroutine() が呼ばれると再入するので注意。
	/// </summary>
	/// <returns>The routine.</returns>
	/// <param name="behaviour">Behaviour.</param>
	/// <param name="methodName">Method name.</param>
	/// <param name="routine">Routine.</param>
	public static MyLib.Coroutine AddRoutine(MyLib.MonoBehaviour behaviour, string methodName, IEnumerator routine)
	{
		MyLib.BehaviourData bdata;

		if (behaviourDict.TryGetValue(behaviour, out bdata))
		{
			var coroutine = new MyLib.Coroutine(methodName, routine);

			// 何はともあれまずコルーチンを登録
			var list = new LinkedList<MyLib.Coroutine>();
			coroutine.node = list.AddLast(coroutine);
			bdata.routineList.AddLast(list);

			// コルーチンの初回実行を行う。
			ProcessCoroutine(coroutine);

			return coroutine;
		}
		else
		{
			// ここに来ることはない
			return null;
		}
	}

	/// <summary>
	/// コルーチンの実行。コルーチンが既に終了していたらfalseを返す。
	/// </summary>
	/// <param name="routineList">Routine list.</param>
	/// <param name="coroutine">Coroutine.</param>
	private static bool ProcessCoroutine(MyLib.Coroutine coroutine)
	{
		// 一回だけ実行
		bool executed = coroutine.routine.MoveNext();

		if (executed)
		{
			object current = coroutine.routine.Current;

			// current は yield return の戻り値である。
			if (current is MyLib.Coroutine)
			{
				MyLib.Coroutine nested = (MyLib.Coroutine)current;

				if (nested.isChained)
				{
					UnityEngine.Debug.Log("[エラー] 1つのコルーチンで2つ以上のコルーチンを待機させる事はできません。");
				}
				else
				{
					// nestedが登録されているLinkedListからnestedを削除。
					nested.node.List.Remove(nested.node);
					// coroutineのリストに改めてnestedを登録。
					nested.node = coroutine.node.List.AddLast(nested);
					// nestedはコルーチンチェーンに組み込まれたので、フラグを立てる。
					nested.isChained = true;
				}
			}
		}

		return executed;
	}

	public static void RemoveRoutine(MyLib.MonoBehaviour behaviour, string methodName)
	{
		MyLib.BehaviourData bdata;
		if (behaviourDict.TryGetValue(behaviour, out bdata))
		{
			LinkedListNode<LinkedList<MyLib.Coroutine>> node = bdata.routineList.First;
			while (node != null)
			{
				LinkedList<MyLib.Coroutine> list = node.Value;
				RemoveRoutineSub(list, methodName);

				var oldNode = node;
				node = node.Next;

				// listの要素が空になった場合は、list自体を除去。
				if (list.Count == 0)
				{
					bdata.routineList.Remove(oldNode);
				}
			}
		}
	}

	private static void RemoveRoutineSub(LinkedList<MyLib.Coroutine> list, string methodName)
	{
		LinkedListNode<MyLib.Coroutine> node = list.First;
		while (node != null)
		{
			var oldNode = node;
			node = node.Next;
			if (oldNode.Value.methodName == methodName)
			{
				list.Remove(oldNode);
			}
		}
	}

	public static void RemoveAllRoutines(MyLib.MonoBehaviour behaviour)
	{
		MyLib.BehaviourData bdata;
		if (behaviourDict.TryGetValue(behaviour, out bdata))
		{
			bdata.routineList.Clear();
		}
	}

	public void Awake()
	{
		AddMonoBehaviour(new Test());
	}

	public void Update()
	{
		// すべてのMonoBehaviourを実行
		foreach (MyLib.BehaviourData bdata in behaviourDict.Values)
		{
			if (!bdata.mainloopBegan)
			{
				bdata.behaviour.Start();
				bdata.mainloopBegan = true;
			}

			bdata.behaviour.Update();
		}

		// すべてのMonoBehaviourが持つコルーチンを実行。
		// コルーチンは Update の後に呼ばれるので、ここで実行。
		foreach (MyLib.BehaviourData bdata in behaviourDict.Values)
		{
			LinkedListNode<LinkedList<MyLib.Coroutine>> node = bdata.routineList.First;
			while (node != null)
			{
				LinkedList<MyLib.Coroutine> coroutineChain = node.Value;
				ProcessChainedCoroutine(coroutineChain);

				var oldNode = node;
				node = node.Next;

				// コルーチンチェーンが空になったら、チェーンの入れ物自体を破棄。
				if (coroutineChain.Count == 0)
				{
					bdata.routineList.Remove(oldNode);
				}
			}
		}
	}

	private void ProcessChainedCoroutine(LinkedList<MyLib.Coroutine> chain)
	{
		// chainの末尾を実行。
		// 実行完了していたら、chainから削除。

		LinkedListNode<MyLib.Coroutine> node = chain.Last;
		if (node != null)
		{
			MyLib.Coroutine coroutine = node.Value;

			if (ProcessCoroutine(coroutine))
			{
				node = node.Next;
			}
			else
			{
				// 終わったコルーチンはリストから除外
				LinkedListNode<MyLib.Coroutine> toRemove = node;
				node = node.Next;
				chain.Remove(toRemove);
			}
		}
	}
}
