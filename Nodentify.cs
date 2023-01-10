using HarmonyLib;
using NeosModLoader;
using FrooxEngine;
using FrooxEngine.LogiX;
using FrooxEngine.UIX;
using BaseX;

namespace Nodentify;
public class Nodentify : NeosMod
{
    public override string Author => "Cyro";
    public override string Name => "Nodentify";
    public override string Version => "1.0.4";

    private static ModConfiguration? _config;
    
    private static double _lastTime;
    private static bool _flag;
    private static double _then;

    [AutoRegisterConfigKey]
    private static ModConfigurationKey<double> _pressDelay = new("PressDelay", "Press delay for node actions",
        () => 0.25);

    [AutoRegisterConfigKey]
    private static ModConfigurationKey<bool> _allowModifiedNodeNames = new("AllowModifiedNodeNames",
        "Allow LogiX node names to be edited and to show custom names", () => true);

    private static void Press(IButton b, ButtonEventData d) { _then = Engine.Current.WorldManager.FocusedWorld.Time.WorldTime; }

    private static void Hold(IButton b, ButtonEventData d, TextEditor e)
    {
        if (e.IsDestroyed || _flag)
            return;
        _lastTime = Engine.Current.WorldManager.FocusedWorld.Time.WorldTime - _then;
        if (!(_lastTime > _config!.GetValue(_pressDelay))) return;
        _flag = true;
        e.Focus();
    }
    private static void RefHold(IButton b, ButtonEventData d, IReferenceNode __instance)
    {
        if (_flag)
            return;
        _lastTime = Engine.Current.WorldManager.FocusedWorld.Time.WorldTime - _then;
        if (!(_lastTime > _config!.GetValue(_pressDelay))) return;
        _flag = true;
        InspectorHelper.OpenInspectorForTarget(__instance.Target ?? __instance, null, __instance.Target != null);
    }
    private static void Release(IButton b, ButtonEventData d)
    {
        _lastTime = 0.0;
        _flag = false;
    }
    public override void OnEngineInit()
    {
        var harmony = new Harmony("net.Cyro.Nodentify");
        _config = GetConfiguration();
        _config!.Save(true);
        harmony.PatchAll();
    }
    [HarmonyPatch(typeof(LogixNode), "GenerateUI")]
    static class LogixNode_GenerateUI_Patch
    {
        private static void Postfix(LogixNode __instance, UIBuilder __result, Slot root, float minWidth = 0f, float minHeight = 0f)
        {
            if (__result.Current == null || !_config!.GetValue(_allowModifiedNodeNames))
                return;

            var t =  __result.Current.GetComponent<Text>();
            if (t == null)
                return;
            var textSlot = t.Slot;
            var instanceSlot = __instance.Slot;
            instanceSlot.Tag = string.IsNullOrEmpty(instanceSlot.Tag) ? null : instanceSlot.Tag;

            var originalText = t.Content.Value;
            t.NullContent.Value = originalText;
            var tagMember = instanceSlot.GetSyncMember("Tag");
            t.Content.DriveFrom((IField<string>)tagMember, true);
            textSlot.Tag = "Nodentify.Node.AlteredText";
            
            var textEditor = textSlot.AttachComponent<TextEditor>();
            textEditor.Text.Target = t;
            textEditor.FinishHandling.Value = TextEditor.FinishAction.NullOnEmpty;
            var b = textSlot.AttachComponent<Button>();
            b.LocalPressed += Press;
            b.LocalPressing += (b, d) => Hold(b, d, textEditor);
            b.LocalReleased += Release;
        }
    }

    [HarmonyPatch(typeof(ReferenceNode<IChangeable>), "OnGenerateVisual")]
    static class ReferenceNode_OnGenerateVisual_Patch
    {
        static void Postfix(IReferenceNode __instance, Slot root)
        {
            var canvas = root.FindChild(s => s.Name == "Canvas");
            var c = canvas?.GetComponent<Canvas>();
            if (c == null) return;
            c.IgnoreTouchesFromBehind.Value = false;
            canvas.Tag = "Nodentify.Node.AlteredRef";
            var b = canvas.AttachComponent<Button>();
            b.LocalPressed += Press;
            b.LocalPressing += (b, d) => RefHold(b, d, __instance);
            b.LocalReleased += Release;
            var col = __instance.GetType().GetGenericArguments()[0].GetColor().SetA(0.8f);
            canvas[0].GetComponent<Image>().Tint.Value = col;
            canvas[0][0].GetComponent<Image>().Tint.Value = col;
        }
    }
    
    [HarmonyPatch(typeof(ComponentBase<Component>), "OnStart")]
    static class ComponentBase_OnStart_Patch
    {
        static void Postfix(Component __instance)
        {
            if (!(__instance is LogixNode) || !(__instance as LogixNode)?.HasVisual == true)
                return;
            
            if (__instance is IReferenceNode)
            {
                var canvas = __instance.Slot.FindChild(s => s.Tag == "Nodentify.Node.AlteredRef");
                if (canvas == null)
                    return;
                
                var reference = __instance as IReferenceNode;
                var refButton = canvas.GetComponent<Button>();
                refButton.LocalPressed += Press;
                refButton.LocalPressing += (b, d) => RefHold(b, d, reference!);
                refButton.LocalReleased += Release;
                return;
            }

            if (!_config!.GetValue(_allowModifiedNodeNames))
                return;
            var s = __instance.Slot.FindChild((s) => s.Tag == "Nodentify.Node.AlteredText", 25);
            if (s == null)
                return;

            var b = s.GetComponent<Button>();
            var textEditor = s.GetComponent<TextEditor>();
            if(b == null || textEditor == null)
                return;
            b.LocalPressed += Press;
            b.LocalPressing += (b, d) => Hold(b, d, textEditor);
            b.LocalReleased += Release;
        }
    }
}
