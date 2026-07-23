// custom-tab-bar/index.js
Component({
  data: {
    selected: 0,
    list: [
      {
        pagePath: "/pages/index/index",
        text: "首页",
        icon: "🏠"
      },
      {
        pagePath: "/pages/students/students",
        text: "学生",
        icon: "👥"
      },
      {
        pagePath: "/pages/lessons/lessons",
        text: "备课",
        icon: "📅"
      },
      {
        pagePath: "/pages/questions/questions",
        text: "错题",
        icon: "❓"
      }
    ]
  },
  methods: {
    switchTab(e) {
      const data = e.currentTarget.dataset;
      wx.switchTab({
        url: data.path
      });
      this.setData({
        selected: data.index
      });
    }
  }
});
