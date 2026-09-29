using UnityEngine;

/// <summary>數字只使用已結算的傷害；合作模式的數值來自 master 廣播。</summary>
public sealed class RogueCombatNumber : MonoBehaviour
{
    public TextMesh Label;
    public float Lifetime = .65f, Rise = 1.1f;
    float age;
    public static void Show(DamageReceiver target, float damage)
    {
        if (target == null || damage <= 0 || target.userIsPlayer) return;
        var role=target.GetComponent<RogueEnemyRole>();if(role!=null)role.lastHitDamage=damage;
        var prefab = Resources.Load<GameObject>("Armory/DamageNumber");
        if (prefab == null) return;
        var body = target.GetComponent<Collider>();
        Vector3 position = body != null ? body.bounds.center + Vector3.up * body.bounds.extents.y * .8f : target.transform.position + Vector3.up * 4;
        if(Camera.main!=null)position+=Camera.main.transform.right;
        var go = Instantiate(prefab, position, Quaternion.identity);
        var number = go.GetComponent<RogueCombatNumber>();
        number.Label.text = Mathf.RoundToInt(damage).ToString();
    }
    void OnWillRenderObject() { if(Camera.current!=null)transform.rotation=Camera.current.transform.rotation; }
    void LateUpdate()
    {
        age += Time.deltaTime;
        if (age >= Lifetime) { Destroy(gameObject); return; }
        transform.position += Vector3.up * (Rise * Time.deltaTime / Lifetime);
        if (Camera.main != null) transform.rotation = Camera.main.transform.rotation;
        var color = Label.color; color.a = 1 - age / Lifetime; Label.color = color;
    }
}
