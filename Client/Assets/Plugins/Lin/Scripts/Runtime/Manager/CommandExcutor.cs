using Cysharp.Text;
using Lin.Runtime.DesignPattern.Singleton;
using Lin.Runtime.Helper;
using Lin.Runtime.Interface;
using Lin.Runtime.Manager;
using System;
using System.Collections.Concurrent;

namespace Lin.Runtime
{
    public class CommandExecutor : Singleton<CommandExecutor>
    {
        // 待执行集合
        private ConcurrentQueue<ICommand> pendingCommands = new ConcurrentQueue<ICommand>();
        
        // 已执行集合，用于Undo操作
        private ConcurrentStack<ICommand> executedCommands = new ConcurrentStack<ICommand>();
        public int executedCommandsCount => executedCommands.Count;
        // 被回滚的命令集合，用于Redo操作
        private ConcurrentStack<ICommand> undoneCommands = new ConcurrentStack<ICommand>();
        public int undoneCommandsCount => undoneCommands.Count;

        public CommandExecutor()
        {
            MonoRunner.GetInstance().AddListener(MonoRunner.EUpdateType.Update, Execute);
        }

        private void Execute()
        {
            // 执行待执行队列中的命令
            while (pendingCommands.TryDequeue(out ICommand command))
            {
                try
                {
                    command.Do();
                    executedCommands.Push(command);
                    // 执行新命令时清空重做栈
                    undoneCommands.Clear();
                }
                catch (Exception ex)
                {
                    this.Error(ZString.Concat("Command execution failed: ", ex.Message));
                }
            }

            // 限制已执行命令栈的大小，防止内存无限增长
            while (executedCommands.Count > 50)
            {
                // 移除最早入栈的command（栈底元素）
                var commandArray = executedCommands.ToArray();
                executedCommands.Clear();

                // 重新入栈，跳过最早的一个（数组最后一个元素是栈底）
                for (int i = commandArray.Length - 2; i >= 0; i--)
                    executedCommands.Push(commandArray[i]);
            }
        }

        /// <summary> 塞进执行队列 </summary>
        public void Do(ICommand command)
        {
            if (command != null)
                pendingCommands.Enqueue(command);
        }

        /// <summary> 撤销执行 </summary>
        public void Undo()
        {
            if (executedCommands.TryPop(out var command))
            {
                try
                {
                    command.Undo();
                    undoneCommands.Push(command);
                }
                catch (Exception ex)
                {
                    this.Error(ZString.Concat("Command undo failed: ", ex.Message));
                }
            }

            while (undoneCommands.Count > 50)
            {
                // 移除最早入栈的command（栈底元素）
                var commandArray = undoneCommands.ToArray();
                undoneCommands.Clear();
                // 重新入栈，跳过最早的一个（数组最后一个元素是栈底）
                for (int i = commandArray.Length - 2; i >= 0; i--)
                {
                    undoneCommands.Push(commandArray[i]);
                }
            }
        }

        /// <summary> 恢复执行 </summary>
        public void Redo()
        {
            if (undoneCommands.TryPop(out var command))
            {
                try
                {
                    command.Do();
                    executedCommands.Push(command);
                }
                catch (Exception ex)
                {
                    this.Error(ZString.Concat("Command redo failed: ", ex.Message));
                }
            }
        }
    }

}
