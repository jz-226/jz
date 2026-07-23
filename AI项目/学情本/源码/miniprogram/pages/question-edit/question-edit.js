// pages/question-edit/question-edit.js
const db = require('../../utils/db');
const util = require('../../utils/util');
const app = getApp();

Page({
  data: {
    id: '',
    students: [],
    studentIndex: 0,
    selectedStudentName: '',
    subjects: [],
    subjectIndex: 0,
    statusOptions: [
      { value: 'pending', label: '待讲解', icon: '⏳' },
      { value: 'explained', label: '已讲解', icon: '✓' },
      { value: 'mastered', label: '已掌握', icon: '★' }
    ],
    form: {
      studentId: '',
      subject: '',
      knowledgePoint: '',
      content: '',
      wrongReason: '',
      correctAnswer: '',
      status: 'pending'
    }
  },

  onLoad(options) {
    this.setData({
      subjects: app.globalData.subjects
    });

    if (options.studentId) {
      this.setData({ 'form.studentId': options.studentId });
    }

    this.loadStudents();

    if (options.id) {
      this.setData({ id: options.id });
      this.loadQuestion(options.id);
    }
  },

  async loadStudents() {
    try {
      const students = await db.students.getAll();
      this.setData({ students });

      if (this.data.form.studentId) {
        const studentIndex = students.findIndex(s => s._id === this.data.form.studentId);
        if (studentIndex >= 0) {
          this.setData({
            studentIndex,
            selectedStudentName: students[studentIndex].name
          });
        }
      }
    } catch (error) {
      console.error('加载学生列表失败:', error);
      util.showError('加载学生列表失败');
    }
  },

  async loadQuestion(id) {
    util.showLoading('加载中...');
    try {
      const question = await db.questions.getById(id);

      const studentIndex = this.data.students.findIndex(s => s._id === question.studentId);
      const subjectIndex = this.data.subjects.findIndex(s => s === question.subject);


      this.setData({
        form: {
          studentId: question.studentId,
          subject: question.subject,
          knowledgePoint: question.knowledgePoint || '',
          content: question.content,
          wrongReason: question.wrongReason || '',
          correctAnswer: question.correctAnswer || '',
          status: question.status || 'pending'
        },
        studentIndex: studentIndex >= 0 ? studentIndex : 0,
        selectedStudentName: studentIndex >= 0 ? this.data.students[studentIndex].name : '',
        subjectIndex: subjectIndex >= 0 ? subjectIndex : 0
      });
    } catch (error) {
      console.error('加载问题失败:', error);
      util.showError('加载失败');
    } finally {
      util.hideLoading();
    }
  },

  onStudentChange(e) {
    const index = e.detail.value;
    this.setData({
      studentIndex: index,
      selectedStudentName: this.data.students[index].name,
      'form.studentId': this.data.students[index]._id
    });
  },

  onSubjectChange(e) {
    const index = e.detail.value;
    this.setData({
      subjectIndex: index,
      'form.subject': this.data.subjects[index]
    });
  },

  onKnowledgePointInput(e) {
    this.setData({ 'form.knowledgePoint': e.detail.value });
  },

  onContentInput(e) {
    this.setData({ 'form.content': e.detail.value });
  },

  onWrongReasonInput(e) {
    this.setData({ 'form.wrongReason': e.detail.value });
  },

  onCorrectAnswerInput(e) {
    this.setData({ 'form.correctAnswer': e.detail.value });
  },

  onStatusTap(e) {
    const value = e.currentTarget.dataset.value;
    this.setData({ 'form.status': value });
  },

  validate() {
    if (!this.data.form.studentId) {
      util.showToast('请选择学生');
      return false;
    }
    if (!this.data.form.subject) {
      util.showToast('请选择科目');
      return false;
    }
    if (!this.data.form.content.trim()) {
      util.showToast('请输入问题内容');
      return false;
    }
    return true;
  },

  async onSubmit() {
    if (!this.validate()) return;

    util.showLoading('保存中...');
    try {
      const data = {
        studentId: this.data.form.studentId,
        subject: this.data.form.subject,
        knowledgePoint: this.data.form.knowledgePoint.trim(),
        content: this.data.form.content.trim(),
        wrongReason: this.data.form.wrongReason.trim(),
        correctAnswer: this.data.form.correctAnswer.trim(),
        status: this.data.form.status
      };

      if (this.data.id) {
        await db.questions.update(this.data.id, data);
      } else {
        await db.questions.add(data);
      }

      await util.showSuccess('保存成功');
      util.navigateBack();
    } catch (error) {
      console.error('保存问题失败:', error);
      util.showError('保存失败');
    } finally {
      util.hideLoading();
    }
  },

  async onDelete() {
    const confirmed = await util.showConfirm('确定要删除这个问题吗？');
    if (!confirmed) return;

    try {
      await db.questions.delete(this.data.id);
      await util.showSuccess('已删除');
      util.navigateBack();
    } catch (error) {
      console.error('删除问题失败:', error);
      util.showError('删除失败');
    }
  }
});
