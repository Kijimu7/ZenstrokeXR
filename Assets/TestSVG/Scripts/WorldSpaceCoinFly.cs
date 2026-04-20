//using UnityEngine;
//using UnityEngine.Pool;
//using DG.Tweening;

//public class WorldSpaceCoinFly3D : MonoBehaviour
//{
//    [SerializeField] private Transform coinPrefab;
//    [SerializeField] private Transform coinParent;
//    [SerializeField] private Transform spawnPoint;
//    [SerializeField] private Transform targetPoint;

//    [SerializeField] private int coinsPerBurst = 6;
//    [SerializeField] private float spawnScatter = 0f;
//    //[SerializeField] private float burstDistance = 0f;
//    [SerializeField] private float burstTime = 0f;
//    [SerializeField] private float flyTime = 2f;
//    [SerializeField] private float stagger = 0.04f;
//    [SerializeField] private float startScale = 20f;
//    [SerializeField] private float midScale = 20f;
//    [SerializeField] private float endScale = 20f;
//    [SerializeField] private float zOffsetTowardCamera = -10f;

//    private ObjectPool<Transform> pool;

//    private void Awake()
//    {
//        pool = new ObjectPool<Transform>(
//            CreateCoin,
//            OnTakeFromPool,
//            OnReturnedToPool,
//            OnDestroyPoolObject,
//            false,
//            10,
//            30
//        );
//    }

//    private Transform CreateCoin()
//    {
//        Transform coin = Instantiate(coinPrefab, coinParent);
//        coin.SetParent(coinParent, false);
//        coin.gameObject.SetActive(false);
//        return coin;
//    }

//    private void OnTakeFromPool(Transform coin)
//    {
//        coin.gameObject.SetActive(true);
//        coin.DOKill();
//    }

//    private void OnReturnedToPool(Transform coin)
//    {
//        coin.DOKill();
//        coin.gameObject.SetActive(false);
//    }

//    private void OnDestroyPoolObject(Transform coin)
//    {
//        Destroy(coin.gameObject);
//    }

//    public void PlayBurst()
//    {
//        if (coinParent == null || spawnPoint == null || targetPoint == null)
//        {
//            Debug.LogWarning("Missing coinParent, spawnPoint, or targetPoint.");
//            return;
//        }

//        Debug.Log($"coinParent local: {coinParent.localPosition}");
//        Debug.Log($"spawn local: {spawnPoint.localPosition}");
//        Debug.Log($"target local: {targetPoint.localPosition}");
//        Debug.Log($"spawn world: {spawnPoint.position}");
//        Debug.Log($"target world: {targetPoint.position}");

//        for (int i = 0; i < coinsPerBurst; i++)
//        {
//            Transform coin = pool.Get();
//            coin.SetParent(coinParent, false);

//            Vector3 startPos = coinParent.InverseTransformPoint(spawnPoint.position) + new Vector3(
//                Random.Range(-spawnScatter, spawnScatter),
//                Random.Range(-spawnScatter, spawnScatter),
//                zOffsetTowardCamera
//            );

//            Vector3 burstPos = startPos + new Vector3(
//                Random.Range(-burstDistance, burstDistance),
//                Random.Range(-burstDistance, burstDistance),
//                0f
//            );

//            Vector3 targetPos = coinParent.InverseTransformPoint(targetPoint.position) + new Vector3(
//                0f,
//                0f,
//                zOffsetTowardCamera
//            );

//            Debug.Log("startPos in coinParent local: " + startPos);
//            Debug.Log("targetPos in coinParent local: " + targetPos);

//            coin.localPosition = startPos;
//            coin.localRotation = Quaternion.identity;
//            coin.localScale = Vector3.one * startScale;

//            Sequence seq = DOTween.Sequence();
//            seq.AppendInterval(i * stagger);

//            seq.Append(coin.DOLocalMove(burstPos, burstTime).SetEase(Ease.OutQuad));
//            seq.Join(coin.DOScale(midScale, burstTime));

//            seq.Append(coin.DOLocalMove(targetPos, flyTime).SetEase(Ease.InOutQuad));
//            seq.Join(coin.DOScale(endScale, flyTime));

//            seq.OnComplete(() =>
//            {
//                pool.Release(coin);
//            });
//        }
//    }
//}