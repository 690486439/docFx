// TODO: support multiple languages: [].concat(langs)
function transform(model, _attrs) {
  if (isString(model)) model = JSON.parse(model);

  // attrs contains additional system infomation:
  if (_attrs && isString(_attrs)) _attrs = JSON.parse(_attrs);

  model = new ViewModelFactory().create(model, _attrs);
  return model;

  function ViewModelFactory() {
    this.create = createViewModel;

    function createViewModel(model, _attrs) {
      if (!model) return null;
      if (isArray(model)) return new TocViewModel(model, _attrs);
      if (!model.items || model.items.length === 0) return null;

      // Pickup the first item and display
      var item = model.items[0];
      var refs = new References(model);
      if (!item.type) return new GeneralViewModel(item, _attrs, refs);
      switch (item.type.toLowerCase()) {
        case 'conceptual':
          return new ConceptualViewModel(item, _attrs, refs);
        case 'namespace':
          return new NamespaceViewModel(item, _attrs, refs);
        default:
          return new ClassViewModel(item, _attrs, refs);
      }
    }
  }

  function GeneralViewModel(item, _attrs, refs) {
    this._navPath = _attrs._navPath;
    this._tocPath = _attrs._tocPath;
    this._pageTitle = _attrs._pageTitle;
    this._title = _attrs._title;
    this._locales = _attrs._locales;
    this._ext = _attrs._ext;
    this._lang = _attrs._lang;
    this._appName =_attrs._name;
    this._rel = _attrs._rel;
    this._tocRel = _attrs._tocRel;
    this._navRel = _attrs._navRel;
    this._description = _attrs._description;
    this._tocTitle = _attrs._tocTitle;
    if (refs) {
      this.item = refs.getViewModel(item.uid, this._lang, changeExt(this._ext));
    }
  }

  function ConceptualViewModel(item, _attrs, refs) {
    GeneralViewModel.call(this, item, _attrs, refs);
    this.isConceptual = true;
    this.isHtml = true;
  }

  function NamespaceViewModel(item, _attrs, refs) {
    GeneralViewModel.call(this, item, _attrs, refs);
    this.isNamespace = true;
    this.isHtml = true;
  }

  function ClassViewModel(item, _attrs, refs) {
    GeneralViewModel.call(this, item, _attrs, refs);
    this.isClass = true;
    this.isHtml = true;
  }

  function TocViewModel(items, _attrs) {
    GeneralViewModel.call(this, items, _attrs);
    this.isToc = true;
    var lang = this._lang;
    var ext = this._ext;
    this.toc = items.map(function (currentValue, index, array) {
      return new Reference(currentValue, null).getTocViewModel(lang, changeExt(ext));
    });;
  }

  function References(model) {
    var references = mapping(model.items, mapping(model.references));
    this.getRefvm = getRefvm;
    this.getViewModel = getViewModel;
    var lang = this._lang;

    function getViewModel(uid, lang, extChanger) {
      var vm = getRefvm(uid, lang, extChanger);
      vm.docurl = getImproveTheDocHref(vm);
      vm.sourceurl = getViewSourceHref(vm);

      if (vm.children) {
        var grouped = {};
        // group children with their type
        vm.children.forEach(function (c) {
          c = getViewModel(c, lang, extChanger);
          var type = c.type;
          if (!grouped[type]) grouped[type] = [];
          grouped[type].push(c);
        })
        vm.children = grouped;
      }

      if (vm.inheritance) {
        vm.inheritance = vm.inheritance.map(function (c, i) {
          var inhe = getRefvm(c, lang, extChanger);
          inhe.index = i;
          return inhe;
        })
      }

      var syntax = vm.syntax;
      if (syntax) {
        if (syntax.parameters) {
          syntax.parameters = syntax.parameters.map(function (currentValue, index, array) {

            currentValue.type = getRefvm(currentValue.type, lang, extChanger);
            return currentValue;
          });
        }
        if (syntax.return) {
          syntax.return.type = getRefvm(syntax.return.type, lang, extChanger);
        }
      }
      return vm;
    }

    function getRefvm(uid, lang, extChanger) {
      if(!isString(uid)) {
        console.error("should be uid format: " + uid);
        return uid;
      }

      return new Reference(references[uid], this).getReferenceViewModel(lang, extChanger);
    }

    /*
      map array to dictionary with uid as the key
    */
    function mapping(arr, obj) {
      if (!arr) return null;
      if (!obj) obj = {};
      for (var i = arr.length - 1; i >= 0; i--) {
        var item = arr[i];
        if (item.uid) obj[item.uid] = item;
      };
      return obj;
    }

    /*
      TODO: integrate with zhyan's change
    */
    function getImproveTheDocHref(item) {
      if (!item || !item.conceputal || !item.conceputal.source || !item.conceputal.source.remote) return '';
      return getRemoteUrl(tem.conceputal.source.remote, tem.conceputal.source.startLine + 1);
    }

    function getViewSourceHref(item) {
      /* jshint validthis: true */
      if (!item || !item.source || !item.source.remote) return '';
      return getRemoteUrl(item.source.remote, item.source.startLine + 1);
    }

    function getRemoteUrl(remote, startLine) {
      if (remote && remote.repo) {
        var repo = remote.repo;
        if (repo.substr(-4) === '.git') {
          repo = repo.substr(0, repo.length - 4);
        }
        var linenum = startLine ? startLine : 0;
        if (repo.match(/https:\/\/.*\.visualstudio\.com\/.*/g)) {
          // TODO: line not working for vso
          return repo + '#path=/' + remote.path;
        }
        if (repo.match(/https:\/\/.*github\.com\/.*/g)) {
          var path = repo + '/blob' + '/' + remote.branch + '/' + remote.path;
          if (linenum > 0) path += '/#L' + linenum;
          return path;
        }
      } else {
        return '';
      }
    }
  }

  function Reference(obj, refs) {
    var _obj = obj;
    this.getReferenceViewModel = getReferenceViewModel;
    this.getTocViewModel = getTocViewModel;

    function getTocViewModel(lang, extChanger) {
      var vm = {};
      // Copy other properties and override name/id
      for (var key in _obj) {
        if (_obj.hasOwnProperty(key)) {
          vm[key] = _obj[key];
        }
      }
      // if homepage is defined, override href with homepage
      if (extChanger) vm.href = extChanger(vm.homepage||vm.href);
      vm.name = getLinkText(vm.href, vm.name);

      if (vm.items) {
        vm.items = _obj.items.map(function (c) {
          return new Reference(c, refs).getTocViewModel(lang, extChanger);
        })
      }

      return vm;
    }

    function getReferenceViewModel(lang, extChanger){
      var displayName = getDisplayName(lang, extChanger);
      var name = getLangSpecifiedProperty.call(_obj, "fullName", lang) || getLangSpecifiedProperty.call(_obj, "name", lang);
      var id = getHtmlId(name);
      var vm = {};

      // Copy other properties and override name/id
      for (var key in _obj) {
        if (_obj.hasOwnProperty(key)) {
          vm[key] = _obj[key];
        }
      }
      vm.name = displayName;
      vm.id = id;
      vm.summary = normalize(vm.summary);
      vm.remarks = normalize(vm.remarks);
      vm.conceptual = normalize(vm.conceptual);

      vm.level = vm.inheritance ? vm.inheritance.length : 0;
      if (vm.syntax) {
        vm.syntax.content = getLangSpecifiedProperty.call(vm.syntax, "content", lang);
      }
      return vm;
    }

    // Replace newline with </br> for markdown tables
    // names such as Tuple<string, int> should already be html-encoded.
    function normalize(content) {
      if (!content) return content;
      return content.replace(/\n/g, '</br>');
    }

    function getHtmlId(input) {
      return input.replace(/\W/g, '_');
    }

    function getDisplayName(lang, extChanger) {
      // spec is always language specific
      var name = '';
      var spec = _obj["spec." + lang];
      if (spec && isArray(spec)) {
        spec.forEach(function (s) {
          // TODO: sanitize s.name first incase it is < or >
          // TODO: what about href?
          name += getCompositeName.call(s, lang, extChanger);
        });

        return name;
      }

      return getCompositeName.call(_obj, lang, extChanger);
    }

    function getCompositeName(lang, extChanger) {
      // If href exists, return name with href, elsewise, return full name
      var href = this.href;
      var fullName = getLangSpecifiedProperty.call(this, "fullName", lang) || getLangSpecifiedProperty.call(this, "name", lang);
      if (!href) return escape(fullName);
      if (extChanger) href = extChanger(href);
      return getLinkText(href, getLangSpecifiedProperty.call(this, "name", lang));
    }

    function getLangSpecifiedProperty(key, lang) {
      return this[key + "." + lang] || this[key];
    }

    function getLinkText(href, name) {
      return '<a href="' + href + '">' + escape(name) + '</a>'
    }
  }

  function escape(str){
    if (!str) return str;
    return Mustache.escape(str);
  }

  function changeExt(e) {
    var ext = e;
    return function (path) {
      // if ext is empty, remove current extension
      // if path ends with '/' or '\', consider it as a folder, extension not added
      if (!path || ext === undefined || path[path.length-1]==='/' || path[path.length-1]==='\\') return path;
      var pathWithoutExt = path.substring(0, path.lastIndexOf('.'));
      if (ext && ext[0] !== '.') return pathWithoutExt + '.' + ext;

      return pathWithoutExt + ext;
    }
  }

  function isArray(arr) {
    if (Object.prototype.toString.call(arr) === '[object Array]') return true;
    return false;
  }

  function isString(input) {
    return typeof input === 'string' || input instanceof String;
  }
}
