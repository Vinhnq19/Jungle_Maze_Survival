using JungleMaze.App.Hud.Feedback;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JungleMaze.Presentation.UI.Hud.Feedback
{
    /// <summary>One pickup toast with an art-swappable placeholder icon.</summary>
    public sealed class PickupToastView:MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;[SerializeField] private TMP_Text label,count,plus;[SerializeField] private Image background,icon;
        private readonly char[] _buffer=new char[24];
        private IItemIconProvider _icons;private string _iconId;private Sprite _placeholder;private Image.Type _placeholderType;private bool _hasPlaceholder,_iconValid;private Color _placeholderColor=Color.white;
        public CanvasGroup Group=>group;public TMP_Text Label=>label;public Image Icon=>icon;
        public void ApplyPalette(HudPalette p){background.color=p.Surface;label.color=p.Text;count.color=p.Muted;plus.color=p.Green;_placeholderColor=p.IconPlaceholder;CapturePlaceholder();icon.color=icon.sprite==_placeholder?_placeholderColor:Color.white;}
        /// <summary>Sets the ID→sprite provider; the current icon is looked up again on the next <see cref="Show(bool,string,string,int,float)"/>.</summary>
        public void ConfigureIcons(IItemIconProvider icons){_icons=icons;_iconValid=false;}
        public void Show(bool visible,string name,int quantity,float alpha)=>Show(visible,null,name,quantity,alpha);
        /// <summary>Shows the row; the icon is looked up only when <paramref name="id"/> changes, unknown IDs fall back to the placeholder.</summary>
        public void Show(bool visible,string id,string name,int quantity,float alpha)
        {
            group.alpha=visible?alpha:0;if(!visible)return;if(label.text!=name)label.text=name;count.SetCharArray(_buffer,0,HudFeedbackFormat.Count(_buffer,quantity));
            if(!_iconValid||!string.Equals(_iconId,id,System.StringComparison.Ordinal))SetIcon(id);
        }
        private void SetIcon(string id)
        {
            CapturePlaceholder();_iconId=id;_iconValid=true;var sprite=_icons!=null&&!string.IsNullOrEmpty(id)?_icons.GetIcon(id):null;var placeholder=sprite==null;if(placeholder)sprite=_placeholder;
            if(icon.sprite!=sprite)icon.sprite=sprite;icon.type=placeholder?_placeholderType:Image.Type.Simple;icon.preserveAspect=!placeholder;icon.color=placeholder?_placeholderColor:Color.white;
        }
        private void CapturePlaceholder(){if(_hasPlaceholder||icon==null)return;_placeholder=icon.sprite;_placeholderType=icon.type;_hasPlaceholder=true;}
    }
}
