using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Docked_AI.Features.MainWindow.State
{
    /// <summary>
    /// 可观察对象基类 - 实现 INotifyPropertyChanged 接口
    /// 
    /// 【文件职责】
    /// 1. 提供 INotifyPropertyChanged 的标准实现
    /// 2. 简化属性变化通知的代码
    /// 3. 作为所有 ViewModel 的基类
    /// 
    /// 【核心设计】
    /// 
    /// 为什么需要 ObservableObject？
    /// - 代码复用：避免在每个 ViewModel 中重复实现 INotifyPropertyChanged
    /// - 标准实现：提供经过验证的属性变化通知逻辑
    /// - 性能优化：SetProperty 方法包含相等性检查，避免不必要的通知
    /// 
    /// 【使用方式】
    /// 
    /// 在 ViewModel 中定义属性：
    /// ```csharp
    /// private string _name;
    /// public string Name
    /// {
    ///     get => _name;
    ///     set => SetProperty(ref _name, value);
    /// }
    /// ```
    /// 
    /// SetProperty 方法的优势：
    /// 1. 自动相等性检查：如果新值与旧值相同，不触发通知
    /// 2. 自动属性名推断：使用 CallerMemberName 自动获取属性名
    /// 3. 返回值指示：返回 true 表示值已更改，false 表示值未更改
    /// 
    /// 【关键依赖关系】
    /// - INotifyPropertyChanged: .NET 标准接口，用于属性变化通知
    /// - CallerMemberName: C# 编译器特性，自动获取调用方法名
    /// 
    /// 【潜在副作用】
    /// 1. PropertyChanged 事件触发（UI 更新）
    /// 2. 如果订阅者很多，可能影响性能
    /// 
    /// 【重构风险点】
    /// 1. SetProperty 的相等性检查：
    ///    - 使用 EqualityComparer<T>.Default.Equals 比较
    ///    - 对于引用类型，比较引用相等性
    ///    - 对于值类型，比较值相等性
    ///    - 如果需要自定义相等性逻辑，需要重写 Equals 方法
    /// 2. PropertyChanged 事件的线程安全：
    ///    - 当前实现不是线程安全的
    ///    - 如果在多线程环境使用，需要添加锁保护
    /// 3. RaisePropertyChanged 方法：
    ///    - 手动触发属性变化通知
    ///    - 用于计算属性或依赖属性
    ///    - 必须传递正确的属性名，否则 UI 不更新
    /// </summary>
    public abstract class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// 设置属性值并触发属性变化通知
        /// 
        /// 【核心逻辑】
        /// 1. 使用 EqualityComparer 比较新值和旧值
        /// 2. 如果相等，返回 false（值未更改）
        /// 3. 如果不相等，更新值并触发 PropertyChanged 事件
        /// 4. 返回 true（值已更改）
        /// 
        /// 【参数说明】
        /// - storage: 属性的后备字段（ref 传递，允许修改）
        /// - value: 新值
        /// - propertyName: 属性名（自动推断，无需手动传递）
        /// 
        /// 【返回值】
        /// true: 值已更改，触发了通知
        /// false: 值未更改，未触发通知
        /// 
        /// 【性能优化】
        /// 相等性检查避免不必要的通知，减少 UI 更新次数
        /// </summary>
        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value))
            {
                return false;
            }

            storage = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }

        /// <summary>
        /// 手动触发属性变化通知
        /// 
        /// 【使用场景】
        /// - 计算属性：属性值依赖其他属性
        /// - 依赖属性：一个属性变化影响多个属性
        /// - 外部更新：属性值由外部代码更新，需要手动通知
        /// 
        /// 【示例】
        /// ```csharp
        /// private string _firstName;
        /// private string _lastName;
        /// 
        /// public string FirstName
        /// {
        ///     get => _firstName;
        ///     set
        ///     {
        ///         if (SetProperty(ref _firstName, value))
        ///         {
        ///             RaisePropertyChanged(nameof(FullName)); // 通知 FullName 变化
        ///         }
        ///     }
        /// }
        /// 
        /// public string FullName => $"{FirstName} {LastName}";
        /// ```
        /// 
        /// 【重要性】
        /// 必须传递正确的属性名，否则 UI 不更新
        /// </summary>
        protected void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
