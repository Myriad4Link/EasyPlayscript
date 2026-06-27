async interface fetch_user_name(user_id: int) : string
async interface log_event(event: string) : void

script async_demo[
正在加载用户信息……

@log_event("demo_started")

用户：@fetch_user_name(42)，你好！

@log_event("demo_finished")
]
