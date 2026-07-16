using UnityEngine;

[ExecuteAlways]
public class MaterialChanger : MonoBehaviour
{
    [SerializeField][Range(0,5)] private float _value = 1;
    [SerializeField] private string _changeMaterialSetting = "_Worn_Level";
    private Renderer[] _renderers;
    private MaterialPropertyBlock _propBlock;
    
    private void OnEnable() => FindAllMaterialInChild();    
    private void Update(){
        _propBlock = new MaterialPropertyBlock();
        SetNewValueForAllMaterial(_value);
    }   
    
    private void FindAllMaterialInChild()
    {
        _renderers = transform.GetComponentsInChildren<Renderer>();
    }
    private void SetNewValueForAllMaterial(float value)
    {
        FindAllMaterialInChild();
        for (int i = 0; i < _renderers.Length; i++){
            _renderers[i].GetPropertyBlock(_propBlock);
            _propBlock.SetFloat(_changeMaterialSetting, value);
            _renderers[i].SetPropertyBlock(_propBlock);
        }                                                                              
    }
}
