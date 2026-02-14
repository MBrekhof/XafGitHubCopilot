using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl.EF;

namespace XafGitHubCopilot.Module.BusinessObjects
{
    [DefaultClassOptions]
    [NavigationItem("Geography")]
    [ImageName("BO_Country")]
    [DefaultProperty(nameof(Name))]
    public class Region : BaseObject
    {
        [StringLength(100)]
        public virtual string Name { get; set; }

        public virtual IList<Territory> Territories { get; set; } = new ObservableCollection<Territory>();
    }
}
